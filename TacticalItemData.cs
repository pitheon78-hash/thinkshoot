using UnityEngine;

/// <summary>
/// [전술 아이템 타입 열거형 - 단일 공식 정의]
/// 프로젝트 전체에서 유일하게 선언된 전역 Enum입니다. 중복 정의(CS0101)를 원천 방지합니다.
/// </summary>
public enum TacticalItemType
{
    None = 0,               // 꽝 (기본 상태)
    Hunter_Grenade = 1,     // 사냥꾼 1번 무기: 특수 유탄 (광역 파괴)
    Hider_DecoyDash = 2,    // 숨는 자: 0.8초 잔상 분신 대시
    Hider_Shield = 3,       // 숨는 자: 0.5초 패링 실드
    Hunter_Piercing = 4     // ★ [신규 추가]: 사냥꾼 2번 무기: 철갑 관통탄 (엄폐물 무시 직격 저격)
}

[System.Serializable]
public struct TacticalItemEntry
{
    public TacticalItemType ItemType;
    [Range(0, 100)]
    public int DropWeight;  // 출현 가중치 (비율)
    public Sprite ItemIcon; // 모바일 UI에 띄울 아이콘
}

[CreateAssetMenu(fileName = "NewItemDropTable", menuName = "Tactical/Item Drop Table")]
public class TacticalItemData : ScriptableObject
{
    [Header("아이템 드롭 테이블 (확률 가중치)")]
    public TacticalItemEntry[] ItemEntries = new TacticalItemEntry[]
    {
        new TacticalItemEntry { ItemType = TacticalItemType.None, DropWeight = 40 },
        new TacticalItemEntry { ItemType = TacticalItemType.Hunter_Grenade, DropWeight = 20 },
        new TacticalItemEntry { ItemType = TacticalItemType.Hunter_Piercing, DropWeight = 20 },
        new TacticalItemEntry { ItemType = TacticalItemType.Hider_DecoyDash, DropWeight = 20 }
    };
}