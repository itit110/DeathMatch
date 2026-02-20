using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
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

    // ────── マウス入力・キー入力 ──────
    private Keyboard kb = Keyboard.current;
    private Mouse ms = Mouse.current;

    // ────── 武器関連 ──────

    [Header("武器関連")]
    [SerializeField] public List<GunController> guns = new List<GunController>();//　銃を配列で取得
    [SerializeField] private int _selectedGun = 0;// 選んだ銃 １；ピストル ２：ショットガン ３：アサルトライフル
    [SerializeField] private float _scrollTime = 0.2f; // 銃の切り替え遅延
    private bool _isSwitcingGun = false;//　遅延フラグ

    [Header("銃弾処理用")]
    [SerializeField] private float _shotTimer;//射撃の間隔
    [SerializeField] public int[] reserveAmmo; //　所持残弾数
    [SerializeField] public int[] maxAmmo;//　最大残弾数
    [Header("マガジン用")]
    [SerializeField] public int[] ammoClip; //　所持残弾数
    [SerializeField] public int[] maxAmmoClip;//　最大残弾数



    private void Start()
    {
        //　カメラオブジェクトを格納
        cam = Camera.main;// メインカメラの場合の処理

        // ────── コンポーネント取得 ──────
        rb = GetComponent<Rigidbody>();

        // ────── R3 通知があった際にのみ処理 ──────

        SetCursorLock(false);// マウスの表示初期セット
        //　マウスカーソル表示非表示
        Observable.EveryUpdate() //　条件を満たした際に通知
            .Where(_ => kb != null && kb.escapeKey.wasPressedThisFrame)//　キー入力がある、かつEscapeキーが押された場合、
            .Subscribe(_ => SetCursorLock(true))//　カーソルを表示
            .RegisterTo(this.GetCancellationTokenOnDestroy());//　後処理

        Observable.EveryUpdate()
            .Where(_ => ms != null && ms.leftButton.wasPressedThisFrame)//　マウス入力があるかつ右クリックが押された場合、
            .Subscribe(_ => SetCursorLock(false))//　カーソルを非表示　
            .RegisterTo(this.GetCancellationTokenOnDestroy());

        // ────── シフトキーで走る ──────
        Observable.EveryUpdate()
            .Where(_ => kb != null && kb.shiftKey.isPressed || kb.leftShiftKey.isPressed)
            .Subscribe(_ => RunAction(true))
            .RegisterTo(this.GetCancellationTokenOnDestroy());

        // ────── 銃の種類をマウスホイールで切り替え ──────
        if (guns.Count > 0) switchGun();

        Observable.EveryUpdate()
            .Select(_ => ms.scroll.ReadValue().y)// Vector2型の変数にマウススクロールの値を取得
            .Where(y => y != 0f) //　マウススクロールの値が0でなければ
            .Subscribe(y => {
                if (!_isSwitcingGun) //　Switch中でなければ
                {
                    SwitchingGuns(y).Forget();
                }
            })
            .RegisterTo(this.GetCancellationTokenOnDestroy());

        // ────── 銃の種類をキー入力で切り替え ──────
        var key1 = Observable.EveryUpdate()
            .Where(_ => kb.digit1Key.wasPressedThisFrame)//　数字キー入力で通知
            .Select(_ => 0);                             //　キー入力された数値から-1の値
        var key2 = Observable.EveryUpdate()
            .Where(_ => kb.digit2Key.wasPressedThisFrame)
            .Select(_ => 1);
        var key3 = Observable.EveryUpdate()
            .Where(_ => kb.digit3Key.wasPressedThisFrame)
            .Select(_ => 2);

        Observable.Merge(key1, key2, key3)//　キー入力された数値を受け取る
            .Subscribe(index =>
            {
                if (!_isSwitcingGun && index < guns.Count)//　もし銃切り替え中ではないかつ要素数より値が多い
                {
                    if (_selectedGun != index)
                    {
                        NumSwitchingGun(index).Forget();//　入力された値を引数で渡す
                    }
                }
            })
            .RegisterTo(this.GetCancellationTokenOnDestroy());

        // ────── 左クリックで弾丸の発射・弾痕の処理 ──────

        Observable.EveryUpdate()//撃てるのかの判定：左クリックが押され、選択中の弾薬が０より多く、経過時間より間隔が長い
            .Where(_ => ms.leftButton.wasPressedThisFrame && ammoClip[_selectedGun] > 0 && Time.time > _shotTimer)
            .Subscribe(_ => FiringBullet())
            .RegisterTo(this.GetCancellationTokenOnDestroy());

        // ────── Rキー入力でリロード ──────
        Observable.EveryUpdate()
            .Where(_ => kb.rKey.wasPressedThisFrame)
            .Subscribe(_ => Reload())
            .RegisterTo(this.GetCancellationTokenOnDestroy());



        //Sequence().Forget();// UniTaskキュー順次実行
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

        // 走っているかの判定：シフトキーが押されていない
        if(IsGround() && kb.shiftKey.isPressed != true || kb.leftShiftKey.isPressed != true)
        {
            RunAction(false); //　デフォルトでは歩き
        }

        if(ms.rightButton.isPressed)
        {
            GunAim(true);
        }
        else
        {
            GunAim(false);
        }

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
        //var kb = Keyboard.current;
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

        // 物理的なジャンプ処理
        var _velocity = rb.linearVelocity;// 推論型 リニアー
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

    public void RunAction(bool run)// シフトキーをしている時は走る
    {
        if(run)//　右シフトもしくは左シフト
        {
            activeMoveSpeed = runSpeed;// 走る
        }
        else
        {
            activeMoveSpeed = walkSpeed;//　歩きに戻る
        }
    }

    public void SetCursorLock(bool _isLocked)
    {
        // マウスを固定し非表示にする処理：FPSで使用
        if (_isLocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if(!_isLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;   
        }
    }

    private async UniTaskVoid SwitchingGuns(float _scrollY)
    {
        if(guns.Count == 0) return;//デバッグ

        _isSwitcingGun = true;//　遅延フラグON

        if (_scrollY > 0f)//　マウスホイールが0より多い
        {
            _selectedGun++;//　銃の入れ替え

            //　カウントでListに格納された要素数を返すList<Gun...>なら３
            if (_selectedGun >= guns.Count)
            {
                _selectedGun = 0;
            }
        }
        else if (_scrollY < 0f)//　マウスホイールが0以内
        {
            _selectedGun--;// 銃を反対へ入れ替える

            if(_selectedGun < 0)
            {
                _selectedGun = guns.Count - 1;// 返ってきた要素数の値から1を引く
            }
        }
        switchGun();
        await UniTask.Delay(TimeSpan.FromSeconds(_scrollTime));//　銃切り替え時の遅延処理

        _isSwitcingGun = false;//　遅延フラグオフ
    }

    // キー入力の番号で銃の切り替え
    private async UniTaskVoid NumSwitchingGun(int gunNum)
    {
        if(guns.Count == 0) return;

        _isSwitcingGun = true;
        _selectedGun = gunNum;

        switchGun();
        await UniTask.Delay(TimeSpan.FromSeconds(_scrollTime));

        _isSwitcingGun =false;
    }

    public void switchGun()
    {
        foreach(GunController gun in guns)//　銃のリストの中をループ
        {
            gun.gameObject.SetActive(false);//　全てを非表示
        }
        if (guns.Count > 0)
        {
            guns[_selectedGun].gameObject.SetActive(true);//　選択中の銃だけを表示
        }
    }

    public void GunAim(bool aim)
    {
        //右クリックで覗き込み
        if (aim)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 
                guns[_selectedGun].adsZoom, 
                guns[_selectedGun].adsSpeed * Time.deltaTime);
        }
        else
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView,
                60,
                guns[_selectedGun].adsSpeed * Time.deltaTime);
        }
    }
 
    public void FiringBullet()//　弾を撃つ関数
    {
        ammoClip[_selectedGun]--; //　選択中の弾をデクリメント

        Ray ray = cam.ViewportPointToRay(new Vector2(0.5f, 0.5f)); //　カメラ中心からレイを飛ばす

        //　レイを飛ばし、ヒットしたオブジェクトの情報をhitに格納する
        if(Physics.Raycast(ray,out RaycastHit hit))
        {
            //Debug.Log("当たったオブジェクトは" + hit.collider.gameObject.name);//　当たったオブジェクトコンソール表示

            //　弾痕を当たった場所へ生成
            GameObject bulletImpactObject = Instantiate(guns[_selectedGun].bulletImpact,//　弾を生成
                hit.point + (hit.normal * 0.02f),//　ぶつかったオブジェクトと重ならないように調節
                Quaternion.LookRotation(hit.normal, Vector3.up));// ぶつかったオブジェクトに対し、Y軸を上（Vector3.up）とし90度の方向へ回転させる

            Destroy(bulletImpactObject, 10f);
        }
        //　射撃後のインターバル
        _shotTimer = Time.time + guns[_selectedGun].shootInterval;
    }

    private void Reload()
    {
        // Rボタンが押されたらリロード
        
            //Reloadで補充する弾薬
            int amountNeed = maxAmmoClip[_selectedGun] - ammoClip[_selectedGun];// macの弾数から現在の弾数を引いて必要な弾数を代入

            //　補充したい弾薬と所持弾薬の比較
            int ammoAvaliable = amountNeed < reserveAmmo[_selectedGun] ? amountNeed : reserveAmmo[_selectedGun];//　どれくらい補充可能か

            //　リロード可能かどうか
            if(amountNeed != 0 && reserveAmmo[_selectedGun] != 0)// 弾薬が満タンの時はリロード不可
            {
                //　所持弾薬からリロードする弾薬を引く
                reserveAmmo[_selectedGun] -= ammoAvaliable;

                //　銃に弾薬をセット
                ammoClip[_selectedGun] += ammoAvaliable;
            }

        
    }



    // ──────────── デバッグ処理 ──────────── 

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

// ──────────── 学習用備忘録 ──────────── 

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
 */
