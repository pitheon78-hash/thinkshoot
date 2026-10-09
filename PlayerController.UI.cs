using UnityEngine;
using Fusion;

/// <summary>
/// [UI Part] 퓨전 세션 및 안전 프로퍼티를 유지하며,
/// PC 에디터의 빠른 테스트를 위해 마우스 룩(Mouse Look)과 모바일 터치를 완벽히 분기한 UI 컨트롤러입니다.
/// </summary>
public partial class PlayerController
{
    private bool _showExitConfirmPopup = false;

    private void ProcessIntegratedHunterInput(ref float deltaX, ref float deltaY, float sensMultiplier)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // ==========================================
        // [PC 에디터 전용 로직]: 진짜 FPS 게임처럼 마우스 이동만으로 에임 회전
        // ==========================================
        
        // 1. 좌클릭 시 마우스 커서 화면 중앙에 가두기 및 숨기기 (몰입감 100%)
        if (Input.GetMouseButtonDown(0) && !_showExitConfirmPopup)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 2. ESC 키 누르면 커서 가두기 풀기 (EXIT 버튼 누르기 용도)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 3. 커서가 잠겨 있을 때만 화면을 회전시킴 (드래그 불필요)
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float rawDeltaX = Input.GetAxisRaw("Mouse X");
            float rawDeltaY = Input.GetAxisRaw("Mouse Y");
            deltaX += rawDeltaX * 2.5f * sensMultiplier;
            deltaY += rawDeltaY * 2.5f * sensMultiplier;
        }
#else
        // ==========================================
        // [모바일 전용 로직]: 기존의 씹힘 방지 멀티터치 스와이프 유지
        // ==========================================
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.position.x >= Screen.width * 0.40f)
                {
                    if (t.phase == TouchPhase.Began)
                    {
                        if (_aimFingerId == -1)
                        {
                            _aimFingerId = t.fingerId;
                            _lastTouchAimPos = t.position;
                        }
                    }
                    else if (t.fingerId == _aimFingerId && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
                    {
                        Vector2 touchDelta = t.position - _lastTouchAimPos;
                        deltaX += touchDelta.x * (0.08f * sensMultiplier);
                        deltaY += touchDelta.y * (0.08f * sensMultiplier);
                        _lastTouchAimPos = t.position;
                    }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        if (t.fingerId == _aimFingerId) _aimFingerId = -1;
                    }
                }
            }
        }
#endif
    }

    private void OnGUI()
    {
        if (!IsLocalControlled || CurrentRole == Role.None) return;

        float sw = Screen.width;
        float sh = Screen.height;
        float cx = sw / 2f;
        float cy = sh / 2f;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = Mathf.Clamp((int)(sh * 0.035f), 18, 32);
        btnStyle.fontStyle = FontStyle.Bold;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = Mathf.Clamp((int)(sh * 0.04f), 20, 36);
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        // ==========================================
        // 1. 공통 상단 EXIT (방 나가기) 버튼 & 팝업
        // ==========================================
        float exitBtnW = sw * 0.12f;
        float exitBtnH = sh * 0.08f;
        GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 0.9f);
        if (GUI.Button(new Rect(sw - exitBtnW - 20, 20, exitBtnW, exitBtnH), "EXIT", btnStyle))
        {
            _showExitConfirmPopup = true;
            // 팝업이 뜨면 PC 에디터 마우스 락을 풀어 클릭 가능하게 만듦
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        GUI.backgroundColor = Color.white;

        if (_showExitConfirmPopup)
        {
            float popW = sw * 0.45f;
            float popH = sh * 0.35f;
            Rect popRect = new Rect((sw - popW) / 2f, (sh - popH) / 2f, popW, popH);
            GUI.Box(popRect, "");
            GUI.Box(popRect, "게임을 종료하고 로비로 나가시겠습니까?", labelStyle);

            float bW = popW * 0.38f;
            float bH = popH * 0.25f;
            float bY = popRect.y + popH - bH - 25;

            GUI.backgroundColor = Color.red;
            if (GUI.Button(new Rect(popRect.x + 25, bY, bW, bH), "나가기 (확인)", btnStyle))
            {
                SafeLobbyLoader.Execute(Runner);
            }

            GUI.backgroundColor = Color.gray;
            if (GUI.Button(new Rect(popRect.x + popW - bW - 25, bY, bW, bH), "취소", btnStyle))
            {
                _showExitConfirmPopup = false;
            }
            GUI.backgroundColor = Color.white;
            return;
        }

        // ==========================================
        // 2. 헌터(Hunter) 전용 UI
        // ==========================================
        if (CurrentRole == Role.Hunter)
        {
            float gap = (_isAiming ? 4f : 8f) + _recoilSpread;
            float length = _isAiming ? 8f : 12f;
            float thickness = _isAiming ? 1.8f : 2.5f;

            Color crossColor = (_hitMarkerTimer > 0f) ? Color.red : new Color(0.2f, 1f, 0.3f, 0.9f);
            Texture2D tex = Texture2D.whiteTexture;
            GUI.color = crossColor;
            GUI.DrawTexture(new Rect(cx - thickness / 2f, cy - gap - length, thickness, length), tex);
            GUI.DrawTexture(new Rect(cx - thickness / 2f, cy + gap, thickness, length), tex);
            GUI.DrawTexture(new Rect(cx - gap - length, cy - thickness / 2f, length, thickness), tex);
            GUI.DrawTexture(new Rect(cx + gap, cy - thickness / 2f, length, thickness), tex);
            GUI.color = Color.white;

            int safeAmmo = AmmoCount;

            GUIStyle ammoStyle = new GUIStyle();
            ammoStyle.fontSize = Mathf.Clamp((int)(sh * 0.035f), 18, 30);
            ammoStyle.fontStyle = FontStyle.Bold;
            ammoStyle.normal.textColor = (safeAmmo == 0 && CurrentSpecialAmmo == TacticalItemType.None) ? Color.red : Color.yellow;
            ammoStyle.alignment = TextAnchor.MiddleRight;

            string ammoBullets = "";
            for (int i = 0; i < safeAmmo; i++) ammoBullets += "■ ";
            if (safeAmmo == 0 && CurrentSpecialAmmo == TacticalItemType.None) ammoBullets = "EMPTY!";
            if (CurrentSpecialAmmo == TacticalItemType.Hunter_Grenade) ammoBullets = "[BAZOOKA LOADED]";
            GUI.Label(new Rect(sw - 350, 85, 320, 40), $"AMMO : {ammoBullets}", ammoStyle);

            float fireBtnSize = Mathf.Min(sw * 0.22f, sh * 0.32f);
            Rect fireBtnRect = new Rect(40, sh - fireBtnSize - 40, fireBtnSize, fireBtnSize);

            GUI.backgroundColor = (CurrentSpecialAmmo == TacticalItemType.Hunter_Grenade) ? 
                new Color(1f, 0.3f, 0.1f, 0.9f) : new Color(0.9f, 0.15f, 0.15f, 0.85f);
            string fireLabel = (CurrentSpecialAmmo == TacticalItemType.Hunter_Grenade) ? "ROCKET\nFIRE!" : "FIRE!";

            bool isFirePressed = false;
            
#if UNITY_EDITOR || UNITY_STANDALONE
            // [PC 전용 로직]: 마우스가 잠겨있는 상태에서 좌클릭 시 무조건 사격 (버튼 누를 필요 없음)
            if (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(0))
            {
                isFirePressed = true;
            }
#else
            // [모바일 전용 로직]: FIRE 버튼 영역 안을 터치해야만 사격
            GUI.RepeatButton(fireBtnRect, fireLabel, btnStyle);
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    Vector2 guiTouchPos = new Vector2(t.position.x, sh - t.position.y);
                    if (fireBtnRect.Contains(guiTouchPos))
                    {
                        isFirePressed = true;
                        break;
                    }
                }
            }
#endif

            if (isFirePressed)
            {
                ExecuteHunterShot();
            }
            
            GUI.backgroundColor = Color.white;

            GUIStyle hintStyle = new GUIStyle();
            hintStyle.fontSize = 16;
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            hintStyle.alignment = TextAnchor.LowerRight;
#if UNITY_EDITOR || UNITY_STANDALONE
            GUI.Label(new Rect(sw - 320, sh - 40, 300, 30), "마우스 조작 | ESC: 잠금 해제", hintStyle);
#else
            GUI.Label(new Rect(sw - 320, sh - 40, 300, 30), "화면 우측 드래그: 에임 회전", hintStyle);
#endif
        }
        // ==========================================
        // 3. 히더(Hider) 전용 UI
        // ==========================================
        else if (CurrentRole == Role.Hider)
        {
            int safeHealth = Health;

            Color hpColor = (safeHealth > 50) ? Color.green : ((safeHealth > 20) ? Color.yellow : Color.red);
            labelStyle.normal.textColor = hpColor;
            GUI.Label(new Rect(cx - 150, sh - 90, 300, 50), $"MY HEALTH : {Mathf.Max(0, safeHealth)}", labelStyle);

            GUIStyle hiderHint = new GUIStyle();
            hiderHint.fontSize = 18;
            hiderHint.normal.textColor = Color.white;
            hiderHint.alignment = TextAnchor.LowerLeft;
            GUI.Label(new Rect(40, sh - 50, 400, 30), "좌측 드래그: 이동 | 아래로 스와이프: 구르기", hiderHint);
        }

        // 피격 붉은 화면
        if (_hiderVignetteTimer > 0f && CurrentRole == Role.Hider)
        {
            Color vigColor = new Color(1f, 0f, 0f, (_hiderVignetteTimer / 0.50f) * 0.4f);
            GUI.color = vigColor;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 승리 금빛 화면
        if (_victoryVignetteTimer > 0f)
        {
            Color vicColor = new Color(1f, 0.8f, 0.2f, (_victoryVignetteTimer / 1.2f) * 0.35f);
            GUI.color = vicColor;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}