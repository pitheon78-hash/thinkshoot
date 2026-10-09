using UnityEngine;
using Fusion;
using System.Collections;

/// <summary>
/// [Tactical Floating Bullet] 공중에 떠다니는 실물 탄환 박스입니다.
/// G키 누를 때 실행되는 PlayerController.SetSpecialAmmoWithSound를 직결 호출하여
/// 아이템 획득 시 100.0% 장전 사운드를 확실하게 울립니다.
/// </summary>
public class TacticalFloatingBox : NetworkBehaviour
{
    [Header("★ 탄환 종류 설정 (Hunter_Piercing 또는 Hunter_Grenade)")]
    [SerializeField] private TacticalItemType _itemType = TacticalItemType.Hunter_Piercing;

    [Header("3D 비행 및 궤도 오프셋 설정")]
    [SerializeField] private float _flightSpeed = 1.4f;
    [SerializeField] private float _zSwingDistance = 7.0f;
    [SerializeField] private float _zCenterOffset = 1.0f;
    [SerializeField] private float _minHeight = 4.0f;
    [SerializeField] private float _heightAmplitude = 1.0f;
    [SerializeField] private float _xSwayWidth = 2.5f;
    [SerializeField] private float _xCenterOffset = -3.5f;
    [SerializeField] private float _phaseOffset = 0.0f;

    private bool _isDestroyedLocal = false;
    private MeshRenderer[] _renderers;
    private Collider _collider;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _collider = GetComponentInChildren<Collider>(true);
    }

    public override void Spawned()
    {
        ResetBullet();
    }

    public void ResetBullet()
    {
        _isDestroyedLocal = false;
        gameObject.SetActive(true);

        if (_renderers != null)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = true;
        }
        if (_collider != null) _collider.enabled = true;
    }

    private void Update()
    {
        if (_isDestroyedLocal) return;

        float timeVal = (Runner != null && Runner.IsRunning) ? Runner.SimulationTime : Time.time;
        float t = (timeVal * _flightSpeed) + _phaseOffset;
        float z = _zCenterOffset + Mathf.Sin(t) * _zSwingDistance;
        float y = _minHeight + Mathf.Abs(Mathf.Cos(t * 0.5f)) * _heightAmplitude;
        float x = _xCenterOffset + Mathf.Cos(t) * _xSwayWidth;

        transform.position = new Vector3(x, y, z);
        transform.Rotate(Vector3.up * 180f * Time.deltaTime, Space.Self);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_OnShotHit(PlayerRef shooter)
    {
        if (_isDestroyedLocal) return;
        _isDestroyedLocal = true;

        RPC_BroadcastDestruction(transform.position, _itemType, shooter);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastDestruction(Vector3 hitPos, TacticalItemType rewardItem, PlayerRef shooter)
    {
        _isDestroyedLocal = true;

        if (_renderers != null)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = false;
        }
        if (_collider != null) _collider.enabled = false;

        if (VFXManager.Instance != null) VFXManager.Instance.PlayExplosion(hitPos);
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayStarCollectSound();

        // ★ [오더 100% 직결]: G키 누를 때 성공하는 바로 그 함수를 1:1로 직접 실행!
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p.CurrentRole == PlayerController.Role.Hunter && p.IsLocalControlled)
            {
                // G키 전용 장착 함수를 직접 호출하여 장전음 100.0% 강제 격발!
                p.SetSpecialAmmoWithSound(rewardItem);
                Debug.Log($"<color=green>[전술 장전]: {rewardItem} 획득 및 G키 장전 함수 직결 완료!</color>");
                break;
            }
        }
    }
}