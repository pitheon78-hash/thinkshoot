using UnityEngine;
using Fusion;

/// <summary>
/// 300줄 제한 원칙을 수호하는 특수 유탄 광역 폭발 전담 독립 컴포넌트입니다.
/// </summary>
public class TacticalGrenadeExploder : NetworkBehaviour
{
    public static TacticalGrenadeExploder Instance { get; private set; }

    [Header("폭발 설정")]
    [SerializeField] private float _explosionRadius = 2.5f; // 약 3x3 벽돌 범위 커버
    private Collider[] _hitBuffer = new Collider[16]; // 모바일 GC 0% 방어 버퍼

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

// ====================================================
    // ★ 수정 전: if (HasStateAuthority) 때문에 P2가 쏘면 무시됨
    // ★ 수정 후: 조건문 삭제! 누구나 폭파 스위치를 누를 수 있음!
    // ====================================================
    public void ExplodeAt(Vector3 impactPoint)
    {
        // 권한 체크 삭제! 누구나 폭파를 외칠 수 있습니다.
        RPC_BroadcastGrenadeExplosion(impactPoint); 
    }

    // ====================================================
    // ★ 수정 2: StateAuthority -> All 로 변경하여 RPC 통신망 완전 개방!
    // ====================================================
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_BroadcastGrenadeExplosion(Vector3 center)
    {
        // (이 아래 특수효과, 사운드, 벽돌 파괴 코드는 그대로 유지)

        // 1. 특수효과 풀링 매니저로 폭발 화염 재생
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.PlayExplosion(center);
        }

        // 2. 사운드 재생
        if (ProceduralSoundManager.Instance != null)
        {
            ProceduralSoundManager.Instance.PlayBlockBreakSound();
        }

        // 3. 반경 내 벽돌 일괄 파괴 (GC 0% NonAlloc 연산)
        int hitCount = Physics.OverlapSphereNonAlloc(center, _explosionRadius, _hitBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null) continue;

            // 벽돌 컴포넌트가 있다면 숨김 처리
            MeshRenderer mr = col.GetComponent<MeshRenderer>();
            if (mr != null && col.CompareTag("Untagged")) // 벽돌 오브젝트 식별
            {
                mr.enabled = false;
                col.enabled = false;
            }
        }
    }
}