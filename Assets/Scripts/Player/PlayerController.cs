using Cysharp.Threading.Tasks;
using R3;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerController : MonoBehaviour
{
    // ────── プレイヤー移動・視点 ──────
    [Header("プレイヤー移動・視点処理")]
    // カメラの親オブジェクト
    [SerializeField] private Transform _viewPoint;// 目線の高さ
    [SerializeField] private float _mouseSensitivity = 1f;// 視点移動速度

    //ユーザーマウス入力変数定義
    private Vector2 _mouseDelta;// マウスの移動量を取得
    private float _verticalMouseInput;// Y軸の回転
    private Camera cam;// カメラ格納用

    // プレイヤーの動き関連
    private Vector3 _moveDir;// 入力された値格納
    private Vector3 _movement;// 進む方向
    [SerializeField] private float activeMoveSpeed = 4.0f;// 移動速度

    // ────── ジャンプ・着地判定・剛体 ──────
    [Header("ジャンプ・着地処理")]
    [SerializeField] private float _jumpInterval = 0.2f;// ジャンプ待機時間
    [SerializeField] public Vector3 jumpForce = new Vector3(0, 6, 0);// ジャンプ力

    public Transform groundCheckPoint;// 足元の判定位置
    public LayerMask groundLayer; //地面レイヤー
    public Rigidbody rb; // 剛体

    private bool _isJumping = false;// ジャンプ中かの判定用フラグ
   
    // ────── 移動スピード ──────
    [Header("走る・移動スピード")]
    [SerializeField] public float walkSpeed = 4f;// 歩く速度
    [SerializeField] public float runSpeed = 8f;//　走る速度


    private void Start()
    {
        //　カメラオブジェクトを格納
        cam = Camera.main;// メインカメラの場合の処理

        // ────── コンポーネント取得 ──────
        rb = GetComponent<Rigidbody>();

        // ────── マウス表示処理R3 ──────
        SetCursorLock(false);// マウスの表示初期セット

        var kb = Keyboard.current;
        var ms = Mouse.current;
        Observable.EveryUpdate()
            .Where(_ => kb != null && kb.escapeKey.wasPressedThisFrame)
            .Subscribe(_ => SetCursorLock(true))
            .RegisterTo(this.GetCancellationTokenOnDestroy());
        Observable.EveryUpdate()
            .Where(_ => ms != null && ms.leftButton.wasPressedThisFrame)
            .Subscribe(_ => SetCursorLock(false))
            .RegisterTo(this.GetCancellationTokenOnDestroy());


        //Sequence().Forget();// UniTaskキュー順次実行
    }


    private void Awake()
    {
        
    }

    private void Update()
    {
        // 視点移動関数呼び出し
        PlayerRotate();

        // 移動関数を呼び出し
        PlayerMove();
        // ジャンプ判定：地面に接地かつスペースキーを押しているかつ空中にいない
        if (IsGround() && Keyboard.current.spaceKey.wasPressedThisFrame && !_isJumping)// wasPressedで連続ジャンプ防止
        {
            JumpAction().Forget();// UniTaskジャンプ関数呼び出し
        }

        RunAction();// シフトで走る

        
    }

    private void LateUpdate()//　カメラ用Update
    {
        // カメラの位置調整（viewPointとカメラの視点を同期）
        cam.transform.position = _viewPoint.position;//カメラの位置
        cam.transform.rotation = _viewPoint.rotation;//回転
       
    }

    public void PlayerMove()
    {
        //キーボードの取得　varは推論型（右辺の型が明確な場合に使用。intやfloatでは明示的）
        var kb = Keyboard.current;
        if (kb == null) return;

        // 水平横移動の動き
        float horizontal = 0;
        if (kb.dKey.isPressed) horizontal += 1;
        if (kb.aKey.isPressed) horizontal -= 1;

        // 前後方向の動き
        float vertical = 0;
        if (kb.wKey.isPressed) vertical += 1;
        if (kb.sKey.isPressed) vertical -= 1;

        // キー入力された値を反映、斜め移動速度の正規化
        _moveDir = new Vector3(horizontal, 0, vertical).normalized;
        // プレイヤーの向いている方向へ移動ベクトルを変換
        _movement = transform.TransformDirection(_moveDir) * activeMoveSpeed;

        // RigitBody：velocityではなくlinearVelocity使用（Y軸の落下速度は維持）
        rb.linearVelocity = new Vector3(_movement.x, rb.linearVelocity.y, _movement.z);
    }

    private void PlayerRotate()
    {
        // マウスの動きを取得
        _mouseDelta = Mouse.current.delta.ReadValue();
       
        // 左右の回転：マウスX軸の動き（体）
        float horizontalRotation = _mouseDelta.x * _mouseSensitivity;//入力されたマウスの動きに値を掛けて水平移動の値を代入
        transform.Rotate(Vector3.up * horizontalRotation);// 取得した値を代入したものをトランスフォームに反映

        // 上下の回転：マウスY軸の動き（カメラのみ）
        _verticalMouseInput -= _mouseDelta.y * _mouseSensitivity; //マウスの動きのY軸の値に動きの値を掛けて目線だけを回す
        _verticalMouseInput = Mathf.Clamp(_verticalMouseInput, -90f, 90);// Mathf.Clamp：数値を上下に動かせる首の角度でコンストレイント

        // 設定したviewPointにクオータニオンをオイラー角(Euler)に変換した値を代入
        _viewPoint.localRotation = Quaternion.Euler(_verticalMouseInput, 0, 0);
        //　クオータニオン：Unity計算用　オイラー角：人間が認識しやすい  
    }

    // 地面に着地しているかつスペースキー入力、連続ジャンプ防止
    private async UniTaskVoid JumpAction()
    {
        _isJumping = true; // ジャンプ中

        //キーボードの取得
        var kb = Keyboard.current;

        // 物理的なジャンプ処理
        var _velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(_velocity.x, 0, _velocity.z);
        rb.AddForce(jumpForce, ForceMode.Impulse);//スペースキーが入力された際に力を加える

        // ジャンプ後クールタイム
        await UniTask.Delay(TimeSpan.FromSeconds(_jumpInterval));

        _isJumping = false;// ジャンプ終了
    }

    public bool IsGround()// 地面についているか判定して真偽を返す
    {
        return Physics.CheckSphere(groundCheckPoint.position, 0.2f, groundLayer);
    }

    public void RunAction()// シフトキーをしている時は走る
    {
        var kb = Keyboard.current;
        if(kb == null) return;

        if(kb.shiftKey.isPressed || kb.leftShiftKey.isPressed)//　右シフトもしくは左シフト
        {
            activeMoveSpeed = runSpeed;
        }
        else
        {
            activeMoveSpeed = walkSpeed;
        }
    }

    public void SetCursorLock(bool _isLocked)
    {
        // マウスを固定し非表示にする処理：FPSで使用
        if (_isLocked)
        {
            Debug.Log("マウス表示");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if(!_isLocked)
        {
            Debug.Log("マウス非表示");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;   
        }
       
       
        
    }


    // ──────────── 非同期処理・デバッグ処理 ──────────── 

    async UniTaskVoid Sequence()//ここに順次処理を書いていく
    {
        Debug.Log("UniTask：ゲーム開始待機");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        await UniTask.Delay(1000);
        Debug.Log("UniTask：準備完了！ゲーム開始");
    }


    //　デバッグ用ギズモの可視化
    private void OnDrawGizmos()
    {
        if(groundCheckPoint != null)//　地面接地チェック
        {
            bool isHit = Physics.CheckSphere(groundCheckPoint.position, 0.2f, groundLayer);
            Gizmos.color = isHit ? Color.yellow : Color.red;//　IF文と同じ要領で色分岐
            Gizmos.DrawWireSphere(groundCheckPoint.position, 0.2f);
        }
    }
}

/*　参照型：(代入元も変化する)
 * List<int> myList：実体がない
 * List<int> myList = new List<int>();  ：初期化し実体化
 * 
 * 型推論使用：var
 * var ListA = new List<int>(); new演算子が必要
 * var ListB = ListA;　　同じ型に代入であればnew不要
 * 
 *　値型：（代入元は変化しない）
 * Vector3 posA = new Vector3(1, 0, 0)  new演算子が必要
 * ver posB = posA;  new演算子不要かつ型推論 
 * 
 * 
 */
