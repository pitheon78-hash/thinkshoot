using UnityEngine;

/// <summary>
/// 300줄 제한 원칙을 준수하는 초경량 아이템 가중치 추첨 전담 매니저입니다.
/// </summary>
public class TacticalItemManager : MonoBehaviour
{
    public static TacticalItemManager Instance { get; private set; }

    [Header("데이터 장부 에셋 연결")]
    public TacticalItemData DropTable;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    /// <summary>
    /// 모바일 GC 0% 가중치 기반 무작위 추첨 알고리즘
    /// </summary>
    public TacticalItemType RollRandomItem()
    {
        if (DropTable == null || DropTable.ItemEntries == null || DropTable.ItemEntries.Length == 0)
        {
            return TacticalItemType.None;
        }

        int totalWeight = 0;
        for (int i = 0; i < DropTable.ItemEntries.Length; i++)
        {
            totalWeight += Mathf.Max(0, DropTable.ItemEntries[i].DropWeight);
        }

        if (totalWeight <= 0) return TacticalItemType.None;

        int randomPoint = Random.Range(0, totalWeight);
        int currentSum = 0;

        for (int i = 0; i < DropTable.ItemEntries.Length; i++)
        {
            currentSum += Mathf.Max(0, DropTable.ItemEntries[i].DropWeight);
            if (randomPoint < currentSum)
            {
                return DropTable.ItemEntries[i].ItemType;
            }
        }

        return TacticalItemType.None;
    }

    /// <summary>
    /// 아이템 아이콘 조회용 헬퍼 함수
    /// </summary>
    public Sprite GetItemIcon(TacticalItemType type)
    {
        if (DropTable == null) return null;
        foreach (var entry in DropTable.ItemEntries)
        {
            if (entry.ItemType == type) return entry.ItemIcon;
        }
        return null;
    }
}