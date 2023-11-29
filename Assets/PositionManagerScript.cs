﻿/*
 * PositionManagerScript.cs
 * 
 * ScreenMove And Cursor Sample for
 *  EasyOpenVRUtil 
 *  https://github.com/gpsnmeajp/EasyOpenVRUtil
 *  EasyOpenVROverlayForUnity
 *  https://sabowl.sakura.ne.jp/gpsnmeajp/unity/EasyOpenVROverlayForUnity/
 * 
 * gpsnmeajp 2019/01/04 v0.02
 * v0.02: ビルドすると位置が原点になる問題に対処
 * v0.01: 公開
 * 
 * These codes are licensed under CC0.
 * http://creativecommons.org/publicdomain/zero/1.0/deed.ja
 */

using UnityEngine;
using EasyLazyLibrary;
using Valve.VR;

public class PositionManagerScript : MonoBehaviour {
    [SerializeField]
    private EasyOpenVROverlayForUnity easyOpenVROverlay; //オーバーレイ表示用ライブラリ
    [SerializeField]
    private RectTransform leftCursorTextRectTransform; //左手カーソル表示用Text
    [SerializeField]
    private RectTransform rightCursorTextRectTransform; //右手カーソル表示用Text
    [SerializeField]
    private RectTransform canvasRectTransform; //全体サイズ計算用Canvas位置情報

    private readonly EasyOpenVRUtil util = new EasyOpenVRUtil(); //姿勢取得ライブラリ

    public Vector3 overlayPosition = new Vector3(0.03f, -0.25f, 0.5f); //HMDの前方50cm、25cm下の位置に表示
    public Vector3 overlayRotation = new Vector3(-20f, 0, 0); //操作しやすいよう-20°傾ける

    private bool isScreenMoving = false; //画面を移動させようとしているか？
    private bool screenMoveWithRight = false; //それが右手で行われているか？

    private bool positionInitialize = true; //位置を初期化するフラグ(完了するとfalseになる)
    
    private Vector3 screenOffsetTransform;
    
    private void Start () {
        //姿勢取得ライブラリを初期化
        util.Init();
   }

    private void Update () {
        //姿勢取得ライブラリが初期化されていないとき初期化する
        //(OpenVRの初期化はeasyOpenVROverlayの方で行われるはずなので待機)
        if (!util.IsReady())
        {
            util.Init();
            return;
        }

        //HMDの位置情報が使えるようになった & 初期位置が初期化されていないとき
        if (util.GetHMDTransform() != null && positionInitialize) 
        {
            //とりあえずUnityスタート時のHMD位置に設定
            //(サンプル用。より適切なタイミングで呼び直してください。
            // OpenVRが初期化されていない状態では原点になってしまいます)
            InitPosition();

            //初期位置初期化処理を停止
            positionInitialize = false;
        }

        UpdateCursorPos();
        HandleInput();
        MoveScreen();
    }

    private void UpdateCursorPos()
    {
        
        //カーソル位置を更新
        //オーバーレイライブラリが返す座標系をCanvasの座標系に変換している。
        //オーバーレイライブラリの座標サイズ(RenderTexture依存)と
        //Canvasの幅・高さが一致する必要がある。
        var sizeDelta = canvasRectTransform.sizeDelta;
        leftCursorTextRectTransform.anchoredPosition = new Vector2(easyOpenVROverlay.LeftHandU - sizeDelta.x / 2f, easyOpenVROverlay.LeftHandV - sizeDelta.y / 2f);
        rightCursorTextRectTransform.anchoredPosition = new Vector2(easyOpenVROverlay.RightHandU - sizeDelta.x / 2f, easyOpenVROverlay.RightHandV - sizeDelta.y / 2f);
    }

    private void MoveScreen()
    {
        //移動モード処理
        if (!isScreenMoving) return;
        //ボタンが一切押されなくなったならば移動モードから抜ける
        if ((!screenMoveWithRight &&
             !util.IsControllerButtonPressed(util.GetLeftControllerIndex(), EVRButtonId.k_EButton_Grip)) ||
            (screenMoveWithRight &&
             !util.IsControllerButtonPressed(util.GetRightControllerIndex(), EVRButtonId.k_EButton_Grip)))
        {
            isScreenMoving = false;
            return;
        }
        var pos = util.GetHMDTransform(); //HMDが有効か調べる
        var cpos = screenMoveWithRight ? util.GetRightControllerTransform():util.GetLeftControllerTransform(); //任意の手の姿勢情報
        //HMDも取得したコントローラ姿勢も有効ならば
        if (pos == null || cpos == null) return;
        //コントローラの姿勢クォータニオンを45度傾けて、オイラー角に変換(しないと意図しない向きになってしまう)
        var rotate = cpos.rotation * Quaternion.AngleAxis(45, Vector3.right);
        var ang = rotate.eulerAngles;

        //コントローラの位置をそのままOverlayの位置に反映
        easyOpenVROverlay.Position = cpos.position - rotate * screenOffsetTransform;; //これが難しい...

        //コントローラの回転を適時反転させてOverlayの回転に反映(こちら向きにする)
        easyOpenVROverlay.Rotation = new Vector3(-ang.x, -ang.y, ang.z);
    }

    //コントローラによる画面移動モードにはいる
    private void HandleInput()
    {
        if (easyOpenVROverlay.LeftHandU > -1f)
        {
            if (!isScreenMoving&&util.IsControllerButtonPressed(util.GetLeftControllerIndex(), EVRButtonId.k_EButton_Grip))
            {
                isScreenMoving = true;
                screenMoveWithRight = false;
                
                var cpos = util.GetLeftControllerTransform();
                UpdateOffset(cpos);
            }
            return;
        }
        if (easyOpenVROverlay.RightHandU > -1f)
        {
            if (!isScreenMoving&&util.IsControllerButtonPressed(util.GetRightControllerIndex(), EVRButtonId.k_EButton_Grip))
            {
                isScreenMoving = true;
                screenMoveWithRight = true;
                var cpos = util.GetLeftControllerTransform();
                UpdateOffset(cpos);
            }
        }
    }

    private void UpdateOffset(EasyOpenVRUtil.Transform baseTransform)
    {
        var rotate = Quaternion.Inverse(baseTransform.rotation * Quaternion.AngleAxis(45, Vector3.right));
        screenOffsetTransform = rotate * (baseTransform.position - easyOpenVROverlay.Position);
    }

    //HMDの位置を基準に操作しやすい位置に画面を出す
    private void InitPosition()
    {
        //HMDの姿勢情報を取得する
        var pos = util.GetHMDTransform();

        //HMDの姿勢情報が無効な場合は
        if (pos == null)
        {
            return; //更新しない
        }

        //HMDの位置に、基準位置とHMD角度を加算したものを、表示位置とする(でないと明後日の方向に移動するため)
        easyOpenVROverlay.Position = pos.position + pos.rotation * overlayPosition;

        //HMDの角度を一部反転したものに、基準角度を加算したものを、表示角度とする
        easyOpenVROverlay.Rotation = (new Vector3(-pos.rotation.eulerAngles.x, -pos.rotation.eulerAngles.y, 0)) + overlayRotation;

    }
}