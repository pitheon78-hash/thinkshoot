using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// 개별 블록, 폭발 드럼통, 별 아이템의 피격 판정을 식별하는 식별 컴포넌트입니다.
/// </summary>
public class GimmickTarget : MonoBehaviour
{
    public int TargetId;
    public MapGimmickBuilder.GimmickType TargetType;

    // ★ 추가할 곳 2: 기존 코드 밑에 시각 연출용 변수와 함수를 추가하세요!
    [Tooltip("총에 맞았을 때 실행할 로컬 시각/물리 이벤트")]
    public UnityEvent<Vector3> OnHitLocalEvent;

    public void TriggerLocalReaction(Vector3 hitDirection)
    {
        OnHitLocalEvent?.Invoke(hitDirection);
    }

    public void Init(int id, MapGimmickBuilder.GimmickType type)
    {
        TargetId = id;
        TargetType = type;
    }

    private void Update()
    {
        // 별 아이템은 제자리에서 회전하여 시각적 어그로 유도
        if (TargetType == MapGimmickBuilder.GimmickType.StarItem && gameObject.activeSelf)
        {
            transform.Rotate(new Vector3(45f, 60f, 30f) * Time.deltaTime);
        }
    }
}