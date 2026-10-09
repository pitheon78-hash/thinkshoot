using UnityEngine;
using Fusion;
using System.Collections;

/// <summary>
/// [히더 전용 황금별 아이템] Fusion 네트워크 안전망을 탑재하여
/// 스폰 전 프로퍼티 접근 에러를 차단하고, 히더가 스치는 즉시 100% 획득되어 1회 방어 실드를 부여합니다.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class HiderStarItem : NetworkBehaviour
{
    [Header("시각 효과")]
    [SerializeField] private float _spinSpeed = 160f;
    [SerializeField] private float _bobHeight = 0.25f;
    [SerializeField] private float _bobSpeed = 3.0f;
    [SerializeField] private float _pickupDistance = 2.0f;

    private Vector3 _basePos;
    private MeshRenderer[] _renderers;
    private Collider _collider;
    private bool _isCollectedLocal = false;

    private void Awake()
    {
        _basePos = transform.position;
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _collider = GetComponentInChildren<Collider>(true);
    }

    private void Update()
    {
        if (_isCollectedLocal) return;

        // 황금빛 회전 및 부유
        transform.Rotate(Vector3.up * _spinSpeed * Time.deltaTime, Space.World);
        float newY = _basePos.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(_basePos.x, newY, _basePos.z);

        // ★ [핵심 에러 방어]: Fusion 네트워크가 정상 준비된 플레이어만 검사
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p == null || p.Object == null || !p.Object.IsValid) continue;

            if (p.CurrentRole == PlayerController.Role.Hider)
            {
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist <= _pickupDistance)
                {
                    RequestCollect();
                    break;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollectedLocal) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null && player.Object != null && player.Object.IsValid)
        {
            if (player.CurrentRole == PlayerController.Role.Hider)
            {
                RequestCollect();
            }
        }
    }

    private void RequestCollect()
    {
        if (_isCollectedLocal) return;
        _isCollectedLocal = true;
        RPC_RequestCollect();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCollect()
    {
        RPC_BroadcastStarCollected();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastStarCollected()
    {
        _isCollectedLocal = true;
        SetVisualActive(false);

        if (ProceduralSoundManager.Instance != null)
        {
            ProceduralSoundManager.Instance.PlayStarCollectSound();
        }

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && p.Object != null && p.Object.IsValid)
            {
                if (p.CurrentRole == PlayerController.Role.Hider)
                {
                    p.ActivateHiderShield();
                    break;
                }
            }
        }
    }

    public void SetVisualActive(bool active)
    {
        if (_renderers != null)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = active;
        }
        if (_collider != null) _collider.enabled = active;
    }

    public void ResetStar()
    {
        _isCollectedLocal = false;
        transform.position = _basePos;
        SetVisualActive(true);
    }
}