using UnityEngine;
using Fusion;
using System.Collections;

/// <summary>
/// [Visual Part] CS1061 에러를 유발하던 구형 사운드 함수 호출을 완전히 걷어내어 메아리 버그를 예방하고,
/// 3.5배 거대 로켓 2단 가속 연출과 무기 메쉬 교체 기능만을 무결점으로 보존한 시각화 전용 클래스입니다.
/// </summary>
public partial class PlayerController : NetworkBehaviour
{
    [Header("사격 반동 및 조준 비주얼")]
    [SerializeField] private Transform _weaponRoot;
    [SerializeField] private float _recoilKickBack = 0.18f;
    [SerializeField] private float _recoilRecoverySpeed = 10f;

    [Header("특수 무기 3D 메쉬 (Bip001 R Hand 자식)")]
    [SerializeField] private GameObject _directBazookaMesh;
    [SerializeField] private GameObject _directSniperMesh;
    [SerializeField] private GameObject _rocketBulletPrefab;
    [SerializeField] private GameObject _muzzleFlashVFX;

    private Vector3 _originalWeaponLocalPos;
    private Vector3 _currentRecoilOffset = Vector3.zero;
    private float _bazookaPostFireTimer = 0f;
    private float _piercingPostFireTimer = 0f;

    public Vector3 WeaponPositionOffset { get; set; } = Vector3.zero;
    public Vector3 WeaponScaleOffset { get; set; } = Vector3.one;
    public Vector3 WeaponRotationOffset { get; set; } = Vector3.zero;

    private void InitializeVisualComponents()
    {
        if (_weaponRoot != null)
        {
            _originalWeaponLocalPos = _weaponRoot.localPosition;
        }

        DisableLegacyLaserObjects();

        if (_localCamera != null)
        {
            _localCamera.nearClipPlane = 0.01f;
        }

        EnsureSpecialWeaponsCached();
    }

    private void DisableLegacyLaserObjects()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == this.transform) continue;

            if (t.name.Equals("LaserSight") || t.name.Contains("Laser_Sight") || t.name.Contains("LegacyLaser"))
            {
                t.gameObject.SetActive(false);
                LineRenderer lr = t.GetComponent<LineRenderer>();
                if (lr != null) lr.enabled = false;
            }
        }

        MonoBehaviour[] allScripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in allScripts)
        {
            if (s != null && s.GetType().Name.Contains("HunterLaserSight"))
            {
                s.enabled = false;
            }
        }
    }

    private void EnsureSpecialWeaponsCached()
    {
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == this.transform) continue;

            string lowerName = t.name.ToLower();

            if (_directBazookaMesh == null && (lowerName.Contains("bazooka") || lowerName.Contains("rpg7") || lowerName.Contains("rocket_launcher")))
            {
                _directBazookaMesh = t.gameObject;
            }

            if (_directSniperMesh == null && (lowerName.Contains("sniper") || lowerName.Contains("russian")))
            {
                _directSniperMesh = t.gameObject;
            }
        }

        if (_directBazookaMesh != null) _directBazookaMesh.SetActive(false);
        if (_directSniperMesh != null) _directSniperMesh.SetActive(false);
    }

    public void UpdateProceduralWalkAnimation()
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            float speedParam = Mathf.Abs(Input.GetAxisRaw("Horizontal"));
            anim.SetFloat("Speed", speedParam);
        }
    }

    public void UpdateProceduralWalkAnimation(float moveInput, bool isGrounded = true)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            float speedParam = Mathf.Abs(moveInput);
            anim.SetFloat("Speed", speedParam);
            anim.SetBool("IsGrounded", isGrounded);
        }
    }

    public void RebuildLegoFigureForCurrentRole()
    {
        Transform visualRoot = transform.Find("Visual") ?? transform;
        BuildGunViewModel(visualRoot);
        DisableLegacyLaserObjects();
    }

    public void BuildGunViewModel(Transform parentRoot = null)
    {
        if (_weaponRoot == null)
        {
            Transform root = parentRoot != null ? parentRoot : (transform.Find("Visual") ?? transform);
            Transform gunHold = root.Find("GunHold") ?? transform.Find("GunHold");
            if (gunHold != null) _weaponRoot = gunHold;
        }

        if (_weaponRoot != null)
        {
            _originalWeaponLocalPos = _weaponRoot.localPosition;
        }
    }

    public void PlayShootAnimation()
    {
        Animator anim = GetComponentInChildren<Animator>();
        bool isBazooka = (CurrentSpecialAmmo == TacticalItemType.Hunter_Grenade);
        bool isPiercing = (CurrentSpecialAmmo == TacticalItemType.Hunter_Piercing);

        if (isBazooka)
        {
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBazookaFireSound();

            if (anim != null)
            {
                anim.ResetTrigger("UpperShoot");
                anim.ResetTrigger("Shoot");
                anim.ResetTrigger("BazookaShoot");
                anim.SetTrigger("BazookaShoot");
                _bazookaPostFireTimer = 1.35f;
            }

            if (IsLocalControlled) StartCoroutine(SpawnAndFlyVisualRocketRoutine());
            _currentRecoilOffset = new Vector3(0, 0, -_recoilKickBack);
        }
        else if (isPiercing)
        {
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlaySniperFireSound();

            if (anim != null)
            {
                anim.ResetTrigger("UpperShoot");
                anim.ResetTrigger("Shoot");
                anim.SetTrigger("Shoot");
            }

            _piercingPostFireTimer = 1.0f;
            _currentRecoilOffset = new Vector3(0, 0, -0.24f);
        }
        else
        {
            // ★ [핵심 수술]: Combat.cs에서 이미 PlayShootSound()를 호출 중이므로 
            // 여기서는 사운드 코드를 완전히 삭제하여 '메아리 버그'와 CS1061 컴파일 에러를 동시 박멸합니다.
            if (anim != null)
            {
                anim.ResetTrigger("UpperShoot");
                anim.SetTrigger("UpperShoot");
                anim.ResetTrigger("Shoot");
                anim.SetTrigger("Shoot");
            }

            _currentRecoilOffset = new Vector3(0, 0, -_recoilKickBack);
        }
    }

    /// <summary>
    /// ★ [로켓 발사 2단 가속 연출]: 처음 0.35초간 저속으로 3.5배 거대하게 눈앞을 지나가며, 이후 급가속!
    /// </summary>
    private IEnumerator SpawnAndFlyVisualRocketRoutine()
    {
        Vector3 spawnPos = transform.position + (Vector3.up * 1.45f) + (transform.right * 0.45f) + (transform.forward * 1.3f);
        Vector3 fireDirection = transform.forward;

        if (_localCamera != null)
        {
            Ray ray = _localCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 70f) ? hit.point : (ray.origin + ray.direction * 70f);
            fireDirection = (targetPoint - spawnPos).normalized;
        }

        if (_muzzleFlashVFX != null)
        {
            GameObject flash = Instantiate(_muzzleFlashVFX, spawnPos, Quaternion.LookRotation(fireDirection));
            Light[] lights = flash.GetComponentsInChildren<Light>(true);
            foreach (var l in lights) l.enabled = false;
            flash.transform.localScale = Vector3.one * 0.08f;
            Destroy(flash, 0.15f);
        }

        GameObject rocketPrefab = _rocketBulletPrefab != null ? _rocketBulletPrefab : Resources.Load<GameObject>("rocket");
        GameObject rocket = null;

        if (rocketPrefab != null)
        {
            Quaternion spawnRot = Quaternion.LookRotation(fireDirection) * Quaternion.Euler(0, 180, 0);
            rocket = Instantiate(rocketPrefab, spawnPos, spawnRot);
        }
        else
        {
            rocket = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rocket.transform.position = spawnPos;
            rocket.transform.rotation = Quaternion.LookRotation(fireDirection);
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            rocket.GetComponent<Renderer>().material = new Material(unlitShader) { color = Color.yellow };
        }

        if (rocket != null)
        {
            Collider[] colls = rocket.GetComponentsInChildren<Collider>(true);
            foreach (var c in colls) Destroy(c);
            Rigidbody[] rbs = rocket.GetComponentsInChildren<Rigidbody>(true);
            foreach (var r in rbs) Destroy(r);

            rocket.transform.localScale = Vector3.one * 3.5f;

            TrailRenderer trail = rocket.AddComponent<TrailRenderer>();
            trail.time = 0.70f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0.04f;
            Shader trailShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            trail.material = new Material(trailShader) { color = new Color(1f, 0.88f, 0.35f, 1.0f) };

            float elapsed = 0f;
            float totalDuration = 0.95f;

            while (elapsed < totalDuration && rocket != null)
            {
                float currentSpeed = (elapsed < 0.35f) ? Mathf.Lerp(4f, 8f, elapsed / 0.35f) : 22f;
                rocket.transform.position += fireDirection * (currentSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (rocket != null) Destroy(rocket);
        }
    }

    private void UpdateVisuals()
    {
        if (_weaponRoot != null)
        {
            _currentRecoilOffset = Vector3.Lerp(_currentRecoilOffset, Vector3.zero, Time.deltaTime * _recoilRecoverySpeed);
            _weaponRoot.localPosition = _originalWeaponLocalPos + _currentRecoilOffset;
        }

        if (_directBazookaMesh == null || _directSniperMesh == null)
        {
            EnsureSpecialWeaponsCached();
        }

        if (_bazookaPostFireTimer > 0f) _bazookaPostFireTimer -= Time.deltaTime;
        if (_piercingPostFireTimer > 0f) _piercingPostFireTimer -= Time.deltaTime;

        bool isBazookaActive = (CurrentSpecialAmmo == TacticalItemType.Hunter_Grenade || _bazookaPostFireTimer > 0f) && (CurrentRole == Role.Hunter);
        bool isPiercingActive = (CurrentSpecialAmmo == TacticalItemType.Hunter_Piercing || _piercingPostFireTimer > 0f) && (CurrentRole == Role.Hunter);

        bool isRifleActive = (!isBazookaActive && !isPiercingActive && CurrentRole == Role.Hunter);
        if (_weaponRoot != null)
        {
            if (_weaponRoot.gameObject.activeSelf != isRifleActive)
            {
                _weaponRoot.gameObject.SetActive(isRifleActive);
            }
        }

        if (_directBazookaMesh != null)
        {
            if (_directBazookaMesh.activeSelf != isBazookaActive) _directBazookaMesh.SetActive(isBazookaActive);
        }

        if (_directSniperMesh != null)
        {
            if (_directSniperMesh.activeSelf != isPiercingActive) _directSniperMesh.SetActive(isPiercingActive);
        }

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.SetBool("IsBazooka", isBazookaActive);
        }

        if (_hiderVignetteTimer > 0f) _hiderVignetteTimer -= Time.deltaTime;
        if (_victoryVignetteTimer > 0f) _victoryVignetteTimer -= Time.deltaTime;
        if (_dangerAlertTimer > 0f) _dangerAlertTimer -= Time.deltaTime;
        if (_hitMarkerTimer > 0f) _hitMarkerTimer -= Time.deltaTime;
    }

    public void SetAimVisualState(bool isAiming)
    {
        _isAiming = isAiming;

        if (_localCamera != null)
        {
            float targetFOV = isAiming ? 25f : 55f;
            _localCamera.fieldOfView = Mathf.Lerp(_localCamera.fieldOfView, targetFOV, Time.deltaTime * 12f);
        }
    }

    public void ResetAimVisualState()
    {
        _isAiming = false;
        if (_localCamera != null) _localCamera.fieldOfView = 55f;
    }
}