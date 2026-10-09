using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// [Core Part] HiderSkills.cs와의 변수 중복(CS0102)을 방지하고,
/// 무기 획득 시 0.8초 장착 딜레이와 반동 초기화를 강제하여 애니메이션 꼬임을 박멸한 메인 컨트롤러입니다.
/// </summary>
public partial class PlayerController : NetworkBehaviour
{
    public enum Role { None, Hunter, Hider }

    #region Network Properties
    [Networked] private int _netPlayerSlot { get; set; }
    private int _localPlayerSlot = 1;
    public int PlayerSlot
    {
        get => (Object != null && Object.IsValid) ? _netPlayerSlot : _localPlayerSlot;
        set
        {
            _localPlayerSlot = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netPlayerSlot = value;
        }
    }

    [Networked] private Role _netCurrentRole { get; set; }
    private Role _localCurrentRole = Role.Hunter;
    public Role CurrentRole
    {
        get => (Object != null && Object.IsValid) ? _netCurrentRole : _localCurrentRole;
        set
        {
            _localCurrentRole = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netCurrentRole = value;
        }
    }

    [Networked] private int _netAmmoCount { get; set; }
    private int _localAmmoCount = 20;
    public int AmmoCount
    {
        get => (Object != null && Object.IsValid) ? _netAmmoCount : _localAmmoCount;
        set
        {
            _localAmmoCount = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netAmmoCount = value;
        }
    }

    [Networked] public Vector2 AimAngles { get; set; }
    [Networked] public float HiderPosX { get; set; }
    [Networked] public NetworkBool IsHit { get; set; }
    [Networked] public NetworkBool IsRolling { get; set; }
    #endregion

    #region Camera & Sway Settings
    [Header("3인칭 카메라 오프셋")]
    private Vector3 _hunterThirdPersonOffset = new Vector3(0.7f, 1.4f, -2.5f); 
    private float _camFollowSmooth = 15f;

    [Header("총기 관성")]
    [SerializeField] private float _spinePitchWeight = 0.65f;
    [SerializeField] private float _swayAmount = 2.4f;       
    [SerializeField] private float _swaySmooth = 9.5f;      
    private Transform _spineBone;
    private Vector2 _currentSwayRot = Vector2.zero;
    private Vector2 _targetSwayRot = Vector2.zero;

    private Camera _localCamera;
    private float _currentFOV = 40f;
    private Vector3 _shakeOffset = Vector3.zero;

    private bool _isCinematicActive = false;
    private Vector3 _cinematicTargetPos;
    private Quaternion _cinematicTargetRot;
    #endregion

    #region Local State Variables
    public bool IsLocalControlled
    {
        get
        {
            if (Runner == null || !Runner.IsRunning || LobbyManager.IsSinglePlayMode)
                return PlayerSlot == 1 || PlayerSlot == 0;
            return Object != null && Object.IsValid && Object.HasInputAuthority;
        }
    }

    private LineRenderer _bulletTracer;
    private float _yaw = 0f;
    private float _pitch = 0f;
    private float _lastLocalShootTime = 0f;
    private int _lastProcessedRound = -1;

    private float _currentHiderVelocity = 0f;
    private bool _isAiming = false;
    
    private float _hitMarkerTimer = 0f;
    private float _recoilSpread = 0f;
    private float _hiderVignetteTimer = 0f;
    private float _dangerAlertTimer = 0f;
    private float _victoryVignetteTimer = 0f;

    private int _moveFingerId = -1;
    private Vector2 _moveTouchStartPos;
    private float _mobileMoveAxisX = 0f;
    private int _aimFingerId = -1;
    private Vector2 _lastTouchAimPos;

    private CapsuleCollider _myCapsule;
    private float _rollTimer = 0f;

    public TacticalItemType CurrentSpecialAmmo { get; private set; }
    #endregion

    #region Initialization & Rendering
    public override void Spawned()
    {
        if (gameObject.name.Contains("P1")) PlayerSlot = 1;
        else if (gameObject.name.Contains("P2")) PlayerSlot = 2;

        _myCapsule = GetComponentInChildren<CapsuleCollider>();

        Shader safeShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");

        if (_bulletTracer == null)
        {
            GameObject bulletObj = new GameObject("BulletTracer_Line");
            _bulletTracer = bulletObj.AddComponent<LineRenderer>();
            _bulletTracer.useWorldSpace = true;
            _bulletTracer.startWidth = 0.04f;
            _bulletTracer.endWidth = 0.01f;
            _bulletTracer.positionCount = 2;
            _bulletTracer.enabled = false;
            
            Material tracerMat = new Material(safeShader);
            tracerMat.color = Color.yellow;
            _bulletTracer.material = tracerMat;
        }

        if (IsLocalControlled) SetupDedicatedLocalCamera();

        AssignInitialRoleDirectly();
        InitializeVisualComponents();
        CacheSpineBone();
        RefreshBodyVisibilityForRole();
    }

    private void CacheSpineBone()
    {
        Transform[] allBones = GetComponentsInChildren<Transform>(true);
        foreach (var t in allBones)
        {
            if (t.name.Contains("Spine") || t.name.Contains("Chest"))
            {
                _spineBone = t;
                break;
            }
        }
    }

    private void SetupDedicatedLocalCamera()
    {
        AudioListener[] existingListeners = FindObjectsOfType<AudioListener>();
        foreach (var l in existingListeners) l.enabled = false;

        _localCamera = Camera.main;
        if (_localCamera == null)
        {
            Camera[] existingCams = FindObjectsOfType<Camera>();
            if (existingCams != null && existingCams.Length > 0) _localCamera = existingCams[0];
        }

        if (_localCamera != null)
        {
            _localCamera.nearClipPlane = 0.03f;
            _localCamera.farClipPlane = 300f;
            AudioListener myListener = _localCamera.GetComponent<AudioListener>();
            if (myListener == null) myListener = _localCamera.gameObject.AddComponent<AudioListener>();
            myListener.enabled = true;
        }
    }

    private void AssignInitialRoleDirectly()
    {
        int hunterSlot = (GameLoopManager.Instance != null) ? GameLoopManager.Instance.CurrentHunterIndex : 1;
        CurrentRole = (PlayerSlot == hunterSlot) ? Role.Hunter : Role.Hider;
        UpdateRoleAndTransform();
    }

    public override void Render()
    {
        if (CurrentRole == Role.None || (GameLoopManager.Instance != null && _lastProcessedRound != GameLoopManager.Instance.RoundNumber))
        {
            if (GameLoopManager.Instance != null) _lastProcessedRound = GameLoopManager.Instance.RoundNumber;
            AssignInitialRoleDirectly();
        }

        if (CurrentRole == Role.Hider)
        {
            transform.position = new Vector3(HiderPosX, 1.0f, 10.0f);
            transform.rotation = Quaternion.Euler(0, 180f, 0);
        }
        else if (CurrentRole == Role.Hunter)
        {
            transform.position = new Vector3(0, 1.2f, -10.0f);
            if (IsLocalControlled) transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            else transform.rotation = Quaternion.Euler(0f, AimAngles.x, 0f);
        }

        UpdateProceduralWalkAnimation();
        UpdateAnimationState();
    }

    public void UpdateRoleAndTransform()
    {
        int hunterSlot = (GameLoopManager.Instance != null) ? GameLoopManager.Instance.CurrentHunterIndex : 1;
        CurrentRole = (PlayerSlot == hunterSlot) ? Role.Hunter : Role.Hider;

        RebuildLegoFigureForCurrentRole();
        CacheSpineBone();

        IsHit = false;
        IsRolling = false;
        AmmoCount = 20;
        Health = 100;
        CurrentSpecialAmmo = TacticalItemType.None;
        _isCinematicActive = false; 
        SetActionLock(false); 
        _yaw = 0f;
        _pitch = 0f;
        _shakeOffset = Vector3.zero;
        _hiderVignetteTimer = 0f;
        _victoryVignetteTimer = 0f;
        _currentHiderVelocity = 0f;

        if (IsLocalControlled)
        {
            if (_localCamera == null) SetupDedicatedLocalCamera();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        RefreshBodyVisibilityForRole();
    }

    public void RefreshBodyVisibilityForRole()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r.gameObject.name.Contains("Precision_LaserSight")) continue;

            if (r is LineRenderer && r.gameObject.name != "BulletTracer_Line")
            {
                r.enabled = false;
                continue;
            }

            r.enabled = true; 
        }

        if (_weaponRoot != null)
        {
            _weaponRoot.gameObject.SetActive(CurrentRole == Role.Hunter);
        }
    }
    #endregion

    #region Update & Input Handling
    void Update()
    {
        if (_hitMarkerTimer > 0f) _hitMarkerTimer -= Time.unscaledDeltaTime;
        if (_hiderVignetteTimer > 0f) _hiderVignetteTimer -= Time.unscaledDeltaTime;
        if (_victoryVignetteTimer > 0f) _victoryVignetteTimer -= Time.unscaledDeltaTime;
        if (_recoilSpread > 0f) _recoilSpread = Mathf.MoveTowards(_recoilSpread, 0f, Time.unscaledDeltaTime * 60f);

        _currentSwayRot = Vector2.Lerp(_currentSwayRot, _targetSwayRot, Time.deltaTime * _swaySmooth);
        _targetSwayRot = Vector2.Lerp(_targetSwayRot, Vector2.zero, Time.deltaTime * _swaySmooth);

        UpdateVisuals();
        HandleDynamicHitbox();

        if (!IsLocalControlled || _isCinematicActive || _isActionLocked) return;

        bool isWaiting = (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Waiting);
        if (isWaiting) return;

        HandleInputMovement();
    }

    private void HandleDynamicHitbox()
    {
        if (_rollTimer > 0f)
        {
            _rollTimer -= Time.deltaTime;
            if (_rollTimer <= 0f)
            {
                if (Object != null && Object.IsValid && Object.HasStateAuthority) IsRolling = false;
            }
        }

        if (_myCapsule != null)
        {
            float targetHeight = (Object != null && Object.IsValid && IsRolling) ? 0.8f : 2.0f;
            float targetCenterY = (Object != null && Object.IsValid && IsRolling) ? 0.4f : 1.0f;

            _myCapsule.height = Mathf.Lerp(_myCapsule.height, targetHeight, Time.deltaTime * 25f);
            _myCapsule.center = Vector3.Lerp(_myCapsule.center, new Vector3(0, targetCenterY, 0), Time.deltaTime * 25f);
        }
    }

    private void HandleInputMovement()
    {
        if (CurrentRole == Role.Hider && !IsHit)
        {
            float targetDirection = 0f;
            if (Input.GetKey(KeyCode.A)) targetDirection += 1f;
            if (Input.GetKey(KeyCode.D)) targetDirection -= 1f;

            ProcessMobileHiderTouch();

            if (Mathf.Abs(_mobileMoveAxisX) > 0.05f) targetDirection = _mobileMoveAxisX;
            _currentHiderVelocity = Mathf.MoveTowards(_currentHiderVelocity, targetDirection, Time.deltaTime * 12.0f);

            float currentSpeed = (Object != null && Object.IsValid && IsRolling) ? 8.5f : 5.0f;

            if (Mathf.Abs(_currentHiderVelocity) > 0.001f)
            {
                if (Object != null && Object.IsValid && Object.HasStateAuthority)
                {
                    HiderPosX = Mathf.Clamp(HiderPosX + _currentHiderVelocity * currentSpeed * Time.deltaTime, -10.0f, 10.0f);
                    transform.position = new Vector3(HiderPosX, 1.0f, 10.0f);
                }
                else if (Object != null && Object.IsValid)
                {
                    transform.position += new Vector3(_currentHiderVelocity * currentSpeed * Time.deltaTime, 0, 0);
                }
                else
                {
                    transform.position += new Vector3(_currentHiderVelocity * currentSpeed * Time.deltaTime, 0, 0);
                }
            }
        }
        else if (CurrentRole == Role.Hunter)
        {
            float deltaX = 0f;
            float deltaY = 0f;
            float sensMultiplier = 1.0f;

            if (Input.GetKeyDown(KeyCode.G))
            {
                SetSpecialAmmoWithSound(TacticalItemType.Hunter_Grenade);
            }

            ProcessIntegratedHunterInput(ref deltaX, ref deltaY, sensMultiplier);

            _targetSwayRot.x = Mathf.Clamp(-deltaX * _swayAmount, -7.0f, 7.0f);
            _targetSwayRot.y = Mathf.Clamp(deltaY * _swayAmount, -5.0f, 5.0f);

            _yaw = Mathf.Clamp(_yaw + deltaX, -45f, 45f);
            _pitch = Mathf.Clamp(_pitch - deltaY, -25f, 25f);

            if (Object != null && Object.IsValid && !Object.HasStateAuthority) RPC_SyncAimAngles(_yaw, _pitch);
        }
    }

    private void ProcessMobileHiderTouch()
    {
        if (Input.touchCount > 0)
        {
            foreach (Touch t in Input.touches)
            {
                if (t.position.x < Screen.width * 0.40f)
                {
                    if (t.phase == TouchPhase.Began)
                    {
                        _moveFingerId = t.fingerId;
                        _moveTouchStartPos = t.position;
                    }
                    else if (t.fingerId == _moveFingerId && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
                    {
                        float dX = t.position.x - _moveTouchStartPos.x;
                        _mobileMoveAxisX = Mathf.Clamp(-dX / 50f, -1f, 1f);

                        float dY = t.position.y - _moveTouchStartPos.y;
                        
                        if (dY < -35f && !IsHit && !IsRolling)
                        {
                            bool isRightRoll = (_mobileMoveAxisX < -0.1f) || (dX < 0);
                            float rollDir = isRightRoll ? -1f : 1f; 
                            _currentHiderVelocity = rollDir * 1.5f;

                            if (Object != null && Object.IsValid && Object.HasStateAuthority)
                            {
                                IsRolling = true;
                                _rollTimer = 0.4f;
                                TriggerDirectionalRoll(isRightRoll);
                            }
                            else if (Object != null && Object.IsValid)
                            {
                                RPC_RequestDirectionalRoll(isRightRoll);
                            }
                            else 
                            {
                                IsRolling = true;
                                _rollTimer = 0.4f;
                                TriggerDirectionalRoll(isRightRoll);
                            }
                            _moveTouchStartPos = t.position;
                        }
                    }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        if (t.fingerId == _moveFingerId)
                        {
                            _moveFingerId = -1;
                            _mobileMoveAxisX = 0f;
                        }
                    }
                }
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SyncAimAngles(float yaw, float pitch) { AimAngles = new Vector2(yaw, pitch); }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestDirectionalRoll(NetworkBool isRightRoll)
    {
        if (!IsRolling && !IsHit)
        {
            IsRolling = true;
            _rollTimer = 0.4f;
            TriggerDirectionalRoll(isRightRoll);
        }
    }

    private void TriggerDirectionalRoll(bool isRight)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            string stateName = isRight ? "RollRight" : "Rolll";
            anim.CrossFade(stateName, 0.1f);
        }
    }

    // ★ [핵심 버그 픽스]: 무기 획득 시 에임 반동 초기화 및 0.8초의 확고한 장착 딜레이 부여!
    public void SetSpecialAmmoWithSound(TacticalItemType item)
    {
        CurrentSpecialAmmo = item;
        if (item == TacticalItemType.Hunter_Grenade)
        {
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBazookaEquipSound();
        }
        ApplyLaserVisualUpdate(item);

        // 이전 연사로 누적된 반동 수치를 0으로 리셋하여 엉뚱한 곳으로 날아가는 현상 차단
        _recoilSpread = 0f;
        // 철칵! 장전 사운드가 끝날 때까지 0.8초간 사격 원천 봉쇄
        _lastLocalShootTime = Time.time + 0.8f;
    }

    public void StartCinematicZoom(Vector3 targetPosition)
    {
        _isCinematicActive = true;
        SetActionLock(true); 

        if (_localCamera == null) return;

        Vector3 dirToCam = (_localCamera.transform.position - targetPosition).normalized;
        dirToCam.y = 0.8f; 
        dirToCam = dirToCam.normalized;

        Vector3 desiredPos = targetPosition + Vector3.up * 0.5f + dirToCam * 2.5f;

        if (Physics.Raycast(targetPosition + Vector3.up * 0.5f, dirToCam, out RaycastHit hit, 2.5f))
        {
            desiredPos = hit.point - dirToCam * 0.4f; 
        }

        _cinematicTargetPos = desiredPos;
        _cinematicTargetRot = Quaternion.LookRotation((targetPosition + Vector3.up * 0.5f) - desiredPos);
    }
    #endregion

    #region LateUpdate & Camera
    private void LateUpdate()
    {
        if (!IsLocalControlled || CurrentRole == Role.None || _localCamera == null) return;

        if (_isCinematicActive)
        {
            _localCamera.fieldOfView = Mathf.Lerp(_localCamera.fieldOfView, 35f, Time.deltaTime * 5f);
            _localCamera.transform.position = Vector3.Lerp(_localCamera.transform.position, _cinematicTargetPos, Time.deltaTime * 4.5f);
            _localCamera.transform.rotation = Quaternion.Slerp(_localCamera.transform.rotation, _cinematicTargetRot, Time.deltaTime * 5f);
            return; 
        }

        if (CurrentRole == Role.Hunter)
        {
            _localCamera.nearClipPlane = 0.03f;
            float targetFOV = 50f;
            _currentFOV = Mathf.Lerp(_currentFOV, targetFOV, Time.unscaledDeltaTime * 12f);
            _localCamera.fieldOfView = _currentFOV;

            Quaternion camRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 targetCamPos = transform.position + (camRotation * _hunterThirdPersonOffset) + _shakeOffset;
            
            _localCamera.transform.position = Vector3.Lerp(_localCamera.transform.position, targetCamPos, Time.unscaledDeltaTime * _camFollowSmooth);
            _localCamera.transform.rotation = camRotation;

            if (_spineBone != null)
            {
                float finalPitch = (_pitch * _spinePitchWeight) + _currentSwayRot.y;
                float finalYawTilt = _currentSwayRot.x * 0.65f;
                _spineBone.Rotate(Vector3.right, finalPitch, Space.Self);
                _spineBone.Rotate(Vector3.forward, finalYawTilt, Space.Self);
            }
        }
        else if (CurrentRole == Role.Hider)
        {
            _localCamera.nearClipPlane = 0.3f;
            _localCamera.fieldOfView = 58f;
            Vector3 targetCamPos = new Vector3(transform.position.x + 0.45f, 3.2f, 13.6f);
            _localCamera.transform.position = Vector3.Lerp(_localCamera.transform.position, targetCamPos, Time.unscaledDeltaTime * 14f);
            _localCamera.transform.LookAt(new Vector3(transform.position.x * 0.15f, 0.9f, 0.0f));
        }
    }
    #endregion
}