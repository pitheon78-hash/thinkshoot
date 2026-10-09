using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// [Combat Part] _isRoundEnding 자물쇠를 폐기하고 GameLoopManager.CurrentState를 직접 참조하여
/// 체력 0 도달 시 게임이 멈추지 않던 버그와 소총 데스 씬 실종 버그를 100% 박멸한 최종 전투 컨트롤러입니다.
/// </summary>
public partial class PlayerController : NetworkBehaviour
{
    #region Combat Properties
    [Header("체력 시스템")]
    [Networked] private int _netHealth { get; set; }
    private int _localHealth = 100;
    public int Health
    {
        get => (Object != null && Object.IsValid) ? _netHealth : _localHealth;
        set
        {
            _localHealth = value;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) _netHealth = value;
        }
    }

    [Networked] public NetworkBool HasHiderShield { get; set; }

    private HunterLaserSight _myLaserSight;
    private GameObject _shieldVisualObj;

    private Coroutine _burstCoroutine;

    private void TriggerHapticFeedback()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrator.Call("vibrate", 30L); 
            }
        }
        catch 
        { 
            Handheld.Vibrate(); 
        }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
    #endregion

    #region Hider Shield
    public void ActivateHiderShield()
    {
        HasHiderShield = true;
        if (Object != null && Object.IsValid) RPC_BroadcastShieldVisual(true);
        else SetLocalShieldVisual(true);
    }

    public void BreakHiderShield()
    {
        HasHiderShield = false;
        if (Object != null && Object.IsValid) RPC_BroadcastShieldVisual(false);
        else SetLocalShieldVisual(false);
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBlockBreakSound();
    }

    private void SetLocalShieldVisual(bool active)
    {
        if (active)
        {
            if (_shieldVisualObj == null)
            {
                _shieldVisualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(_shieldVisualObj.GetComponent<Collider>());
                _shieldVisualObj.transform.SetParent(transform);
                _shieldVisualObj.transform.localPosition = new Vector3(0, 1.0f, 0);
                _shieldVisualObj.transform.localScale = Vector3.one * 1.8f;

                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(1.0f, 0.85f, 0.1f, 0.35f);
                _shieldVisualObj.GetComponent<Renderer>().material = mat;
            }
            _shieldVisualObj.SetActive(true);
        }
        else
        {
            if (_shieldVisualObj != null) _shieldVisualObj.SetActive(false);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_BroadcastShieldVisual(bool active)
    {
        SetLocalShieldVisual(active);
    }
    #endregion

    #region Hunter Shot Execution
    
    public void ApplyLaserVisualUpdate()
    {
        ApplyLaserVisualUpdate(CurrentSpecialAmmo);
    }

    public void ApplyLaserVisualUpdate(TacticalItemType ammoType)
    {
        if (_myLaserSight == null) _myLaserSight = GetComponentInChildren<HunterLaserSight>(true);
        if (_myLaserSight != null)
        {
            if (ammoType == TacticalItemType.Hunter_Grenade)
            {
                _myLaserSight.gameObject.SetActive(false);
            }
            else
            {
                _myLaserSight.gameObject.SetActive(true);
                _myLaserSight.UpdateLaserMode(ammoType);
            }
        }
    }

    public void ExecuteHunterShot()
    {
        if (CurrentRole != Role.Hunter || _isActionLocked) return;

        if (Time.time < _lastLocalShootTime) return;

        if (AmmoCount <= 0 && CurrentSpecialAmmo == TacticalItemType.None)
        {
            _lastLocalShootTime = Time.time + 0.25f;
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayDryFireSound();
            return;
        }

        if (CurrentSpecialAmmo == TacticalItemType.None)
        {
            if (_burstCoroutine == null)
            {
                _lastLocalShootTime = Time.time + 0.45f;
                _burstCoroutine = StartCoroutine(BurstFireRoutine());
            }
        }
        else
        {
            _lastLocalShootTime = Time.time + 1.0f;
            FireSingleProjectile(CurrentSpecialAmmo);
        }
    }

    private IEnumerator BurstFireRoutine()
    {
        for (int i = 0; i < 3; i++)
        {
            if (AmmoCount <= 0 || _isActionLocked || CurrentSpecialAmmo != TacticalItemType.None) break;
            
            // ★ [핵심 픽스]: 게임이 종료되었으면(Playing 상태가 아니면) 더 이상 총알이 나가지 못하게 즉각 코루틴 캔슬!
            if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState != GameLoopManager.GameState.Playing) break;

            FireSingleProjectile(TacticalItemType.None);
            yield return new WaitForSeconds(0.08f); 
        }
        _burstCoroutine = null;
    }

    private void CreateLocalBulletHole(Vector3 pos, Vector3 normal, Transform parent)
    {
        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hole.name = "BulletHole_Decal";
        Destroy(hole.GetComponent<Collider>());
        
        hole.transform.position = pos + normal * 0.015f; 
        hole.transform.rotation = Quaternion.LookRotation(-normal);
        hole.transform.localScale = Vector3.one * Random.Range(0.12f, 0.18f); 
        
        Material mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
        mat.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        hole.GetComponent<Renderer>().material = mat;
        
        if (parent != null) hole.transform.SetParent(parent);
        Destroy(hole, 5.0f);

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(flash.GetComponent<Collider>());
        flash.transform.position = pos + normal * 0.02f;
        flash.transform.rotation = Quaternion.LookRotation(-normal);
        flash.transform.localScale = Vector3.one * 0.35f;
        
        Material flashMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
        flashMat.color = new Color(1f, 0.8f, 0.1f, 1f);
        flash.GetComponent<Renderer>().material = flashMat;
        if (parent != null) flash.transform.SetParent(parent);
        
        StartCoroutine(HitFlashFadeRoutine(flash, flashMat));
    }

    private IEnumerator HitFlashFadeRoutine(GameObject flash, Material mat)
    {
        float elapsed = 0f;
        float duration = 0.12f; 
        Vector3 startScale = flash.transform.localScale;
        
        while(elapsed < duration)
        {
            if (flash == null) break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            flash.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            Color c = mat.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            mat.color = c;
            yield return null;
        }
        if (flash != null) Destroy(flash);
    }

    private void FireSingleProjectile(TacticalItemType ammoType)
    {
        Camera cam = _localCamera ?? Camera.main;
        if (cam == null) return;

        float currentSpread = _isAiming ? (_recoilSpread * 0.005f) : (_recoilSpread * 0.015f);
        if (ammoType != TacticalItemType.None) currentSpread = 0f;

        Vector2 spreadOffset = Random.insideUnitCircle * currentSpread;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f + spreadOffset.x, 0.5f + spreadOffset.y, 0));

        _recoilSpread = Mathf.Clamp(_recoilSpread + 12f, 0f, 40f);

        _pitch -= 1.5f; 
        if (_weaponRoot != null)
        {
            Vector3 pushBack = new Vector3(0, 0.06f, -0.3f);
            StartCoroutine(WeaponRecoilVisualRoutine(pushBack));
        }

        TriggerHapticFeedback();
        StartCoroutine(CameraShakeRoutine(0.06f, _isAiming ? 0.03f : 0.06f));
        
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayShootSound();
        StartCoroutine(DelayedCasingSoundRoutine());
        
        PlayShootAnimation();

        if (VFXManager.Instance != null)
        {
            Vector3 muzzlePos = (_weaponRoot != null && _weaponRoot.gameObject.activeSelf) ? 
                _weaponRoot.position + (_weaponRoot.forward * 0.65f) : 
                transform.position + (transform.up * 1.25f) + (transform.forward * 0.85f);
            
            VFXManager.Instance.PlayMuzzleFlash(muzzlePos, transform.forward);
            VFXManager.Instance.SpawnLocalBulletCasing(muzzlePos, transform.right, transform.up, transform.forward);
        }

        if (ammoType == TacticalItemType.Hunter_Grenade)
        {
            Vector3 rocketTarget = Physics.Raycast(ray.origin, ray.direction, out RaycastHit rHit, 100f) ? rHit.point : (ray.origin + ray.direction * 60f);
            Vector3 rocketMuzzle = transform.position + (Vector3.up * 1.45f) + (transform.right * 0.45f) + (transform.forward * 1.5f);
            
            if (Object != null && Object.IsValid) RPC_RequestRocketShot(rocketMuzzle, rocketTarget);
            else ExecuteLocalRocketShot(rocketMuzzle, rocketTarget);

            SetSpecialAmmoWithSound(TacticalItemType.None);
            return;
        }

        if (ammoType == TacticalItemType.Hunter_Piercing)
        {
            if (Object != null && Object.IsValid) RPC_RequestPiercingShot(ray.origin, ray.direction);
            else ExecuteLocalPiercingShot(ray.origin, ray.direction);

            SetSpecialAmmoWithSound(TacticalItemType.None);
            return;
        }

        if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit localHit, 100f))
        {
            CreateLocalBulletHole(localHit.point, localHit.normal, localHit.collider.transform);

            PhysXBrickWall hitBrick = localHit.collider.GetComponent<PhysXBrickWall>();
            PreciseBrickGimmick preciseBrick = localHit.collider.GetComponent<PreciseBrickGimmick>();

            if (hitBrick != null)
            {
                hitBrick.TriggerPhysicalBreak(localHit.point, ray.direction, 0.25f, 6.0f);
                if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayHitSound();
            }
            else if (preciseBrick != null)
            {
                preciseBrick.BreakBrick(ray.direction);
                if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBlockBreakSound();
            }

            HeavyBoxGimmick heavyBox = localHit.collider.GetComponentInParent<HeavyBoxGimmick>();
            if (heavyBox != null && (Object == null || !Object.IsValid))
            {
                heavyBox.PushBox(ray.direction);
            }

            TacticalFloatingBox floatingBox = localHit.collider.GetComponentInParent<TacticalFloatingBox>();
            if (floatingBox != null && Object != null && Object.IsValid)
            {
                floatingBox.RPC_OnShotHit(Object.InputAuthority);
            }
        }

        string hitColliderName = "";
        if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit nameHit, 100f))
        {
            hitColliderName = nameHit.collider.name;
        }

        if (Object != null && Object.IsValid)
        {
            RPC_RequestSingleShot(ray.origin, ray.direction, hitColliderName);
        }
        else
        {
            ExecuteLocalSingleShot(ray.origin, ray.direction, hitColliderName);
        }
    }

    private IEnumerator WeaponRecoilVisualRoutine(Vector3 pushBackOffset)
    {
        if (_weaponRoot == null) yield break;
        Vector3 origPos = _weaponRoot.localPosition;
        _weaponRoot.localPosition += pushBackOffset;
        yield return new WaitForSeconds(0.04f);
    }

    private IEnumerator DelayedCasingSoundRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayHitSound();
    }

    private void ExecuteLocalSingleShot(Vector3 origin, Vector3 dir, string hitColliderName)
    {
        if (AmmoCount <= 0) return;
        AmmoCount--;

        Vector3 targetPoint = origin + dir * 60f;
        Vector3 hitNormal = -dir;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, 80f))
        {
            targetPoint = hit.point;
            hitNormal = hit.normal;

            PhysXBrickWall brick = hit.collider.GetComponent<PhysXBrickWall>();
            PreciseBrickGimmick preciseBrick = hit.collider.GetComponent<PreciseBrickGimmick>();

            if (brick != null)
            {
                brick.TriggerPhysicalBreak(hit.point, dir, 0.25f, 6.0f);
            }
            else if (preciseBrick != null)
            {
                preciseBrick.BreakBrick(dir);
            }
            else
            {
                TacticalFloatingBox floatingBox = hit.collider.GetComponentInParent<TacticalFloatingBox>();
                if (floatingBox != null && Runner != null && Runner.IsRunning)
                {
                    floatingBox.RPC_OnShotHit(Runner.LocalPlayer);
                }

                HeavyBoxGimmick heavyBox = hit.collider.GetComponentInParent<HeavyBoxGimmick>();
                if (heavyBox != null)
                {
                    heavyBox.PushBox(dir);
                }

                GimmickTarget gimmick = hit.collider.GetComponent<GimmickTarget>();
                if (gimmick != null)
                {
                    if (gimmick.TargetType == MapGimmickBuilder.GimmickType.ExplosiveBarrel)
                    {
                        if (MapGimmickBuilder.Instance != null) MapGimmickBuilder.Instance.TriggerBarrelExplosion(hit.point, 2.8f);
                        gimmick.gameObject.SetActive(false);
                        if (VFXManager.Instance != null) VFXManager.Instance.PlayExplosion(hit.point);
                    }
                }
                else
                {
                    PlayerController target = hit.collider.GetComponentInParent<PlayerController>();
                    if (target != null && target != this)
                    {
                        // ★ [핵심 픽스]: 게임이 Playing 상태일 때만 데미지를 입혀서 체력이 마이너스로 뚫리는 버그 방지
                        if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
                        {
                            bool isHeadShot = !string.IsNullOrEmpty(hitColliderName) && hitColliderName.ToLower().Contains("head");
                            int damage = isHeadShot ? 15 : 5;
                            target.Health -= damage;
                            
                            _hitMarkerTimer = (target.Health <= 0) ? 0.35f : 0.15f;
                            TriggerHapticFeedback();
                            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayHitSound();

                            // 로컬(오프라인 연습모드) 데스 씬 즉각 호출
                            if (target.Health <= 0) CheckAndHandleRoundEnd(true);
                        }
                    }
                }
            }
        }

        StartCoroutine(TracerRoutine(origin, targetPoint, hitNormal));
        
        // ★ 잔탄 0발(AmmoOut) 체크를 가장 마지막으로 미루어 로직 엉킴 방지
        if (AmmoCount <= 0 && CurrentSpecialAmmo == TacticalItemType.None && GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
        {
            CheckAndHandleRoundEnd(false);
        }
    }

    private void ExecuteLocalRocketShot(Vector3 muzzle, Vector3 target)
    {
        StartCoroutine(RocketFlightRoutine(muzzle, target));
    }

    private void ExecuteLocalPiercingShot(Vector3 origin, Vector3 dir)
    {
        Vector3 beamEnd = origin + dir * 60f;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, 60f);
        foreach (var hit in hits.OrderBy(h => h.distance))
        {
            PhysXBrickWall brick = hit.collider.GetComponent<PhysXBrickWall>();
            if (brick != null) { brick.AwakenPhysics(hit.point, dir, 10.0f); continue; }

            PlayerController target = hit.collider.GetComponentInParent<PlayerController>();
            if (target != null && target != this)
            {
                if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
                {
                    beamEnd = hit.point;
                    target.Health -= 40;
                    _hitMarkerTimer = 0.2f;
                    if (target.Health <= 0) CheckAndHandleRoundEnd(true);
                }
                break;
            }
        }
        StartCoroutine(PiercingBeamRoutine(origin, beamEnd));
    }
    #endregion

    #region Server RPCs & Hit Logic

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastHeavyBoxPush(int boxId, Vector3 hitDir)
    {
        HeavyBoxGimmick[] boxes = FindObjectsOfType<HeavyBoxGimmick>();
        foreach (var b in boxes)
        {
            if (b.BoxId == boxId)
            {
                b.PushBox(hitDir);
                break;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSingleShot(Vector3 origin, Vector3 dir, string hitColliderName)
    {
        if (AmmoCount <= 0) return;
        AmmoCount--;

        Vector3 targetPoint = origin + dir * 60f;
        Vector3 hitNormal = -dir;
        bool hiderKilled = false;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, 80f))
        {
            targetPoint = hit.point;
            hitNormal = hit.normal;

            PhysXBrickWall brick = hit.collider.GetComponent<PhysXBrickWall>();
            PreciseBrickGimmick preciseBrick = hit.collider.GetComponent<PreciseBrickGimmick>();

            if (brick != null)
            {
                brick.TriggerPhysicalBreak(hit.point, dir, 0.25f, 6.0f);
            }
            else if (preciseBrick != null)
            {
                preciseBrick.BreakBrick(dir);
            }
            else
            {
                TacticalFloatingBox floatingBox = hit.collider.GetComponentInParent<TacticalFloatingBox>();
                if (floatingBox != null)
                {
                    floatingBox.RPC_OnShotHit(Object.InputAuthority);
                }

                HeavyBoxGimmick heavyBox = hit.collider.GetComponentInParent<HeavyBoxGimmick>();
                if (heavyBox != null)
                {
                    RPC_BroadcastHeavyBoxPush(heavyBox.BoxId, dir);
                }

                GimmickTarget gimmick = hit.collider.GetComponent<GimmickTarget>();
                if (gimmick != null)
                {
                    if (gimmick.TargetType == MapGimmickBuilder.GimmickType.ExplosiveBarrel)
                    {
                        if (MapGimmickBuilder.Instance != null) MapGimmickBuilder.Instance.TriggerBarrelExplosion(hit.point, 2.8f);
                        RPC_BroadcastBarrelDestroy(gimmick.transform.position);
                    }
                }
                else
                {
                    PlayerController target = hit.collider.GetComponentInParent<PlayerController>();
                    if (target != null && target != this)
                    {
                        // ★ [핵심 픽스]: 게임이 Playing 상태일 때만 서버에서 데미지를 깎아 무한 킬 방지
                        if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
                        {
                            bool isHeadShot = !string.IsNullOrEmpty(hitColliderName) && hitColliderName.ToLower().Contains("head");
                            int damage = isHeadShot ? 15 : 5;

                            target.Health -= damage;
                            RPC_TriggerHitFeedback(target.Health <= 0);
                            target.RPC_TriggerHiderDamageVignette();

                            if (target.Health <= 0)
                            {
                                target.IsHit = true;
                                hiderKilled = true;
                            }
                        }
                    }
                }
            }
        }

        RPC_BroadcastTracer(origin, targetPoint, hitNormal);
        
        // ★ [핵심 픽스]: 킬을 냈거나 잔탄이 0이 되었을 때만 서버에서 라운드 종료 통보
        if (hiderKilled || (AmmoCount <= 0 && CurrentSpecialAmmo == TacticalItemType.None))
        {
            if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
            {
                CheckAndHandleRoundEnd(hiderKilled);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastBarrelDestroy(Vector3 pos)
    {
        Collider[] hits = Physics.OverlapSphere(pos, 1.5f);
        foreach (var h in hits)
        {
            GimmickTarget gt = h.GetComponent<GimmickTarget>();
            if (gt != null && gt.TargetType == MapGimmickBuilder.GimmickType.ExplosiveBarrel)
            {
                gt.gameObject.SetActive(false);
            }
        }
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBlockBreakSound();
        if (VFXManager.Instance != null) VFXManager.Instance.PlayExplosion(pos);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPiercingShot(Vector3 origin, Vector3 dir)
    {
        Vector3 beamEnd = origin + dir * 60f;
        bool hiderKilled = false;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, 60f);
        var sortedHits = hits.OrderBy(h => h.distance);

        foreach (var hit in sortedHits)
        {
            PhysXBrickWall brick = hit.collider.GetComponent<PhysXBrickWall>();
            if (brick != null)
            {
                brick.AwakenPhysics(hit.point, dir, 10.0f);
                continue;
            }

            PlayerController target = hit.collider.GetComponentInParent<PlayerController>();
            if (target != null && target != this)
            {
                if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
                {
                    beamEnd = hit.point;
                    target.Health -= 40;
                    RPC_TriggerHitFeedback(target.Health <= 0);
                    target.RPC_TriggerHiderDamageVignette();

                    if (target.Health <= 0)
                    {
                        target.IsHit = true;
                        hiderKilled = true;
                    }
                }
                break;
            }
        }

        RPC_BroadcastPiercingBeam(origin, beamEnd);
        if (hiderKilled) CheckAndHandleRoundEnd(true);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRocketShot(Vector3 muzzle, Vector3 target)
    {
        RPC_BroadcastRocketFlight(muzzle, target);
        // 바주카는 쏘자마자 끝이 아니라, 폭발 후의 로직에서 hiderKilled를 판정합니다.
    }

    private void CreateMassiveExplosionVFX(Vector3 pos)
    {
        GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(fireball.GetComponent<Collider>());
        fireball.transform.position = pos;
        
        Material mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
        mat.color = new Color(1.0f, 0.4f, 0.0f, 1f); 
        fireball.GetComponent<Renderer>().material = mat;
        
        StartCoroutine(FireballExpandRoutine(fireball, mat));
    }

    private IEnumerator FireballExpandRoutine(GameObject fireball, Material mat)
    {
        float elapsed = 0f;
        float duration = 0.35f; 
        
        while(elapsed < duration)
        {
            if (fireball == null) break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            fireball.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 8.0f, Mathf.Sqrt(t));
            
            Color c = mat.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            mat.color = c;
            yield return null;
        }
        if (fireball != null) Destroy(fireball);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestAreaExplosion(Vector3 explosionPoint, float radius)
    {
        bool hiderKilled = false;
        Collider[] hits = Physics.OverlapSphere(explosionPoint, radius);
        foreach (var h in hits)
        {
            PhysXBrickWall brick = h.GetComponent<PhysXBrickWall>();
            if (brick != null) brick.AwakenPhysics(explosionPoint, Vector3.up, 14.0f);

            PreciseBrickGimmick preciseBrick = h.GetComponent<PreciseBrickGimmick>();
            if (preciseBrick != null) preciseBrick.BreakBrick((preciseBrick.transform.position - explosionPoint).normalized);

            Rigidbody rb = h.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.AddExplosionForce(14.0f, explosionPoint, radius, 0.40f, ForceMode.Impulse);
            }

            PlayerController target = h.GetComponentInParent<PlayerController>();
            if (target != null && target != this)
            {
                if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
                {
                    target.Health -= 60;
                    target.RPC_TriggerHiderDamageVignette();
                    if (target.Health <= 0)
                    {
                        target.IsHit = true;
                        hiderKilled = true;
                    }
                }
            }
        }
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBlockBreakSound();
        CreateMassiveExplosionVFX(explosionPoint);
        
        if (hiderKilled) CheckAndHandleRoundEnd(true);
        else CheckAndHandleRoundEnd(false); // 바주카는 맞추든 못 맞추든 1발 쏘고 터지면 턴 종료
    }
    #endregion

    #region Round End & Animation Triggers
    
    // ★ [핵심 수술]: 어설픈 _isRoundEnding을 폐기하고, GameLoopManager에 직접 보고합니다.
    private void CheckAndHandleRoundEnd(bool hiderKilled)
    {
        if (GameLoopManager.Instance != null)
        {
            // Playing 상태일 때만 라운드 종료 통보를 넘겨서 무한 루프 데드락 방지
            if (GameLoopManager.Instance.CurrentState == GameLoopManager.GameState.Playing)
            {
                GameLoopManager.Instance.CurrentState = GameLoopManager.GameState.RoundEnd;
                
                if (hiderKilled) StartCoroutine(DelayedKillRoundEndRoutine());
                else StartCoroutine(DelayedAmmoOutRoundEndRoutine());
            }
        }
    }

    private IEnumerator DelayedKillRoundEndRoutine()
    {
        RPC_BroadcastEndMotions(true);
        yield return new WaitForSeconds(1.5f);
        RPC_NotifyRoundEnd(true);
    }

    private IEnumerator DelayedAmmoOutRoundEndRoutine()
    {
        RPC_BroadcastEndMotions(false);
        yield return new WaitForSeconds(1.5f);
        RPC_NotifyRoundEnd(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastEndMotions(bool hiderKilled)
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.SetActionLock(true);
            Animator anim = p.GetComponentInChildren<Animator>();
            if (anim == null) continue;

            if (hiderKilled)
            {
                if (p.CurrentRole == Role.Hunter) anim.SetTrigger("Victory");
                else if (p.CurrentRole == Role.Hider)
                {
                    anim.SetTrigger("Die");
                    Collider col = p.GetComponentInChildren<Collider>();
                    if (col != null) col.enabled = false;
                    
                    PlayerController hunter = System.Array.Find(players, x => x.CurrentRole == Role.Hunter);
                    if (hunter != null && hunter.IsLocalControlled) hunter.StartCinematicZoom(p.transform.position);
                }
            }
            else
            {
                if (p.CurrentRole == Role.Hider) anim.SetTrigger("Victory");
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyRoundEnd(bool hiderKilled)
    {
        HasHiderShield = false;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.SetActionLock(false);
            p.IsHit = false;
            p.Health = 100;

            Collider col = p.GetComponentInChildren<Collider>();
            if (col != null) col.enabled = true;

            Animator anim = p.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
                anim.Play("assault_combat_idle", 0, 0f);
            }
        }

        if (GameLoopManager.Instance != null) GameLoopManager.Instance.ApplyScore(hiderKilled);
        bool iAmHunter = (CurrentRole == Role.Hunter);
        bool iWon = (hiderKilled && iAmHunter) || (!hiderKilled && !iAmHunter);

        if (IsLocalControlled && iWon)
        {
            _victoryVignetteTimer = 1.2f;
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayVictoryFanfareSound();
        }
    }
    #endregion

    #region VFX & Broadcasters
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastRocketFlight(Vector3 start, Vector3 end) { StartCoroutine(RocketFlightRoutine(start, end)); }

    private IEnumerator RocketFlightRoutine(Vector3 start, Vector3 end)
    {
        yield return new WaitForSeconds(0.85f);
        if (Object != null && Object.IsValid && Object.HasStateAuthority) RPC_RequestAreaExplosion(end, 3.5f);
        else if (Object == null || !Object.IsValid)
        {
            Collider[] hits = Physics.OverlapSphere(end, 3.5f);
            foreach (var h in hits)
            {
                PhysXBrickWall brick = h.GetComponent<PhysXBrickWall>();
                if (brick != null) brick.AwakenPhysics(end, Vector3.up, 14.0f);

                PreciseBrickGimmick preciseBrick = h.GetComponent<PreciseBrickGimmick>();
                if (preciseBrick != null) preciseBrick.BreakBrick((preciseBrick.transform.position - end).normalized);

                Rigidbody rb = h.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(14.0f, end, 3.5f, 0.40f, ForceMode.Impulse);
                }
            }
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBlockBreakSound();
            CreateMassiveExplosionVFX(end);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastPiercingBeam(Vector3 start, Vector3 end)
    {
        StartCoroutine(PiercingBeamRoutine(start, end));
    }

    private IEnumerator PiercingBeamRoutine(Vector3 start, Vector3 end)
    {
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayBlockBreakSound();
        if (_bulletTracer != null)
        {
            _bulletTracer.material.color = new Color(0.85f, 0.15f, 1.0f, 1.0f);
            _bulletTracer.startColor = new Color(0.9f, 0.2f, 1.0f, 1.0f);
            _bulletTracer.endColor = new Color(0.7f, 0.0f, 1.0f, 0.8f);
            _bulletTracer.startWidth = 0.35f;
            _bulletTracer.endWidth = 0.02f;
            _bulletTracer.SetPosition(0, start);
            _bulletTracer.SetPosition(1, end);
            _bulletTracer.enabled = true;
        }
        SpawnImpactSparkBurst(end, -(end - start).normalized);
        yield return new WaitForSeconds(0.15f);
        if (_bulletTracer != null) _bulletTracer.enabled = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastTracer(Vector3 start, Vector3 end, Vector3 normal) { StartCoroutine(TracerRoutine(start, end, normal)); }

    private IEnumerator TracerRoutine(Vector3 start, Vector3 end, Vector3 normal)
    {
        Vector3 muzzlePos = (_weaponRoot != null && _weaponRoot.gameObject.activeSelf) ? 
            _weaponRoot.position + (_weaponRoot.forward * 0.65f) : 
            transform.position + (transform.up * 1.25f) + (transform.forward * 0.85f);

        GameObject tracerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(tracerDot.GetComponent<Collider>());
        tracerDot.transform.position = muzzlePos;
        tracerDot.transform.localScale = Vector3.one * 0.1f; 
        
        Material dotMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
        dotMat.color = new Color(1f, 1f, 0.8f, 1f); 
        tracerDot.GetComponent<Renderer>().material = dotMat;

        TrailRenderer trail = tracerDot.AddComponent<TrailRenderer>();
        trail.time = 0.08f;
        trail.startWidth = 0.06f;
        trail.endWidth = 0.01f;
        trail.material = dotMat;

        float speed = 350f; 
        float distance = Vector3.Distance(muzzlePos, end);
        float travelTime = distance / speed;
        float elapsed = 0f;

        while (elapsed < travelTime)
        {
            if (tracerDot == null) break;
            elapsed += Time.deltaTime;
            tracerDot.transform.position = Vector3.Lerp(muzzlePos, end, elapsed / travelTime);
            yield return null;
        }

        if (tracerDot != null)
        {
            tracerDot.transform.position = end;
            Destroy(tracerDot);
        }

        if (!Object.HasStateAuthority)
        {
            Collider[] cols = Physics.OverlapSphere(end, 0.3f);
            foreach (var c in cols)
            {
                PhysXBrickWall b = c.GetComponent<PhysXBrickWall>();
                if (b != null) b.TriggerPhysicalBreak(end, -normal, 0.25f, 6.0f);

                PreciseBrickGimmick p = c.GetComponent<PreciseBrickGimmick>();
                if (p != null) p.BreakBrick(-normal);
            }
            if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayHitSound();
        }

        SpawnImpactSparkBurst(end, normal);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_TriggerHiderDamageVignette()
    {
        if (IsLocalControlled) { _hiderVignetteTimer = 0.50f; TriggerHapticFeedback(); }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerHiderDangerAlert()
    {
        if (IsLocalControlled) _dangerAlertTimer = 0.5f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerHitFeedback(bool isKill)
    {
        if (IsLocalControlled && CurrentRole == Role.Hunter)
        {
            _hitMarkerTimer = isKill ? 0.35f : 0.15f;
            TriggerHapticFeedback();
        }
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            _shakeOffset = new Vector3(x, y, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _shakeOffset = Vector3.zero;
    }

    private void SpawnImpactSparkBurst(Vector3 hitPoint, Vector3 normal)
    {
        Material sparkMat = new Material(Shader.Find("Sprites/Default"));
        sparkMat.color = new Color(1.0f, 0.85f, 0.2f, 1f);
        for (int i = 0; i < 4; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(spark.GetComponent<Collider>());
            spark.transform.position = hitPoint + normal * 0.05f;
            spark.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);
            spark.GetComponent<Renderer>().material = sparkMat;
            Vector3 sparkVelocity = (normal + Random.insideUnitSphere * 0.6f).normalized * Random.Range(4.0f, 7.5f);
            StartCoroutine(SparkFlyRoutine(spark, sparkVelocity));
        }
    }

    private IEnumerator SparkFlyRoutine(GameObject spark, Vector3 velocity)
    {
        float elapsed = 0f, duration = 0.14f;
        while (elapsed < duration && spark != null)
        {
            spark.transform.position += velocity * Time.unscaledDeltaTime;
            spark.transform.localScale = Vector3.Lerp(spark.transform.localScale, Vector3.zero, elapsed / duration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (spark != null) Destroy(spark);
    }
    #endregion
}