using UnityEngine;
using Fusion;

/// <summary>
/// [조준 레이저 시스템] 사냥꾼 본인(HasInputAuthority)의 화면에서만 정밀 조준선을 렌더링하며,
/// 관통탄 장착 시 보라색으로 즉시 전환합니다. 히더 화면 시야를 가리는 오염을 100% 차단합니다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class HunterLaserSight : MonoBehaviour
{
    [Header("레이저 기본 색상 설정")]
    [SerializeField] private Color _defaultColor = Color.red;
    [SerializeField] private Color _piercingColor = new Color(0.85f, 0.15f, 1.0f, 1.0f); // 네온 보라색

    private LineRenderer _lineRenderer;
    private PlayerController _ownerPlayer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _ownerPlayer = GetComponentInParent<PlayerController>();
        SetLaserColor(_defaultColor);
    }

    private void Update()
    {
        if (_lineRenderer == null) return;

        // ★ [핵심 차단]: Fusion 입력 권한을 가진 내 사냥꾼일 때만 조준선을 렌더링!
        bool isMyHunter = (_ownerPlayer != null && 
                           _ownerPlayer.Object != null && 
                           _ownerPlayer.Object.HasInputAuthority && 
                           _ownerPlayer.CurrentRole == PlayerController.Role.Hunter);

        if (!isMyHunter)
        {
            if (_lineRenderer.enabled) _lineRenderer.enabled = false;
            return;
        }

        if (!_lineRenderer.enabled) _lineRenderer.enabled = true;

        Camera cam = Camera.main;
        Vector3 rayOrigin = transform.position;
        Vector3 rayDir = (cam != null) ? cam.transform.forward : transform.forward;
        Vector3 endPoint = rayOrigin + rayDir * 50f;

        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, 50f))
        {
            endPoint = hit.point;
        }

        _lineRenderer.SetPosition(0, rayOrigin);
        _lineRenderer.SetPosition(1, endPoint);
    }

    public void UpdateLaserMode(TacticalItemType ammoType)
    {
        if (ammoType == TacticalItemType.Hunter_Piercing)
        {
            SetLaserColor(_piercingColor);
            _lineRenderer.startWidth = 0.08f;
            _lineRenderer.endWidth = 0.08f;
        }
        else
        {
            SetLaserColor(_defaultColor);
            _lineRenderer.startWidth = 0.04f;
            _lineRenderer.endWidth = 0.04f;
        }
    }

    private void SetLaserColor(Color col)
    {
        if (_lineRenderer == null) return;
        _lineRenderer.startColor = col;
        _lineRenderer.endColor = col;
        if (_lineRenderer.material != null)
        {
            _lineRenderer.material.color = col;
        }
    }
}