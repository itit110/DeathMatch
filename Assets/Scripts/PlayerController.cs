using Cysharp.Threading.Tasks;
using R3;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // カメラの親オブジェクト
    [SerializeField] private Transform viewPoint;// 目線の高さ
    [SerializeField] private float mouseSensitivity = 0.01f;// 視点移動速度

    //ユーザーマウス入力
    private Vector2 _mouseDelta;// マウスの移動量を取得
    private float _verticalMouseInput;// Y軸の回転

    // カメラ格納
    private Camera cam;

    // プレイヤーの動き関連
    private Vector3 moveDir;// 入力された値格納
    private Vector3 movement;// 進む方向
    private float activeMoveSpeed = 4.0f;// 移動速度

    private void Start()
    {
        //Que().Forget();// UniTaskキュー順次実行

        //カメラをオブジェクトに格納
        cam = Camera.main;// メインカメラの場合の処理

        //マウスを固定し非表示にする処理：FPSで使用
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
    }

    private void Update()
    {
        // 視点移動関数呼び出し
        PlayerRotate();

        // 移動関数を呼び出し
        PlayerMove();
    }

    private void LateUpdate()
    {
        // カメラの位置調整（viewPointとカメラの視点を同期）
        cam.transform.position = viewPoint.position;//カメラの位置
        cam.transform.rotation = viewPoint.rotation;//回転
       
    }

    public void PlayerMove()
    {
        //キーボードの取得
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

        moveDir = new Vector3(horizontal, 0, vertical).normalized;// キー入力された値を反映、斜め移動用の正規化

        //movement = (transform.forward * moveDir.z) + (transform.right * moveDir.x);
        movement = transform.TransformDirection(moveDir) * activeMoveSpeed;

        transform.position += movement * Time.deltaTime;
    }

    private void PlayerRotate()
    {
        // マウスの動きを取得
        _mouseDelta = Mouse.current.delta.ReadValue();
       
        // 左右の回転：マウスX軸の動き
        float horizontalRotation = _mouseDelta.x * mouseSensitivity;//入力されたマウスの動きに値を掛けて水平移動の値を代入
        transform.Rotate(Vector3.up * horizontalRotation);// 取得した値を代入したものをトランスフォームに反映

        // 上下の回転：マウスY軸の動き
        _verticalMouseInput -= _mouseDelta.y * mouseSensitivity; //マウスの動きのY軸の値に動きの値を掛けて目線だけを回す
        _verticalMouseInput = Mathf.Clamp(_verticalMouseInput, -90f, 90);// 上下に動かせる首の角度 Mathf.Clampで数値を丸める

        // 設定したviewPointにクオータニオンをオイラー角(Euler)風にした値を代入
        viewPoint.localRotation = Quaternion.Euler(_verticalMouseInput, 0, 0);
          
    }

    async UniTask Que()
    {
        //ここに順次処理を書いていく
        Debug.Log("UniTask：ゲーム開始待機");
        await UniTask.Delay(1000);
        Debug.Log("UniTask：準備完了！ゲーム開始");
    }

}

// R3：入力読み取り時にだけ通知
/* ToDo R3まだ上手く使用できず 
inputSubject
    .Subscribe(_ =>
    {
        if(Mouse.current != null) // Null防止
            {
                _mouseDelta = Mouse.current.delta.ReadValue();
            _verticalMouseInput = _mouseDelta.y;
            }
    })
    .RegisterTo(this.GetCancellationTokenOnDestroy());

Observable.IntervalFrame(1)
    .Subscribe(_ => inputSubject.OnNext(Unit.Default))
    .RegisterTo(this.GetCancellationTokenOnDestroy());
*/