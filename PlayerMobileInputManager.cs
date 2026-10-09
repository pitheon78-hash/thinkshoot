using UnityEngine;

/// <summary>
/// [Mobile Input & PC Key Binder] 모바일 터치 및 PC 키보드 입력을 총괄하며,
/// 1번(바주카) 및 2번(철갑 관통탄) 단축키 장전을 완벽하게 바인딩합니다.
/// </summary>
public class PlayerMobileInputManager : MonoBehaviour
{
    public static PlayerMobileInputManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        PlayerController localHunter = null;
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p.IsLocalControlled && p.CurrentRole == PlayerController.Role.Hunter)
            {
                localHunter = p;
                break;
            }
        }

        if (localHunter == null) return;

        // 키보드 1번: 바주카(RPG-7) 강제 장전
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            localHunter.SetSpecialAmmo(TacticalItemType.Hunter_Grenade);
            Debug.Log("<color=orange>[전술 무기]: 바주카(RPG-7 로켓) 장전 완료!</color>");
        }

        // 키보드 2번: 철갑 관통탄(레일건 빔) 즉시 장전
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            localHunter.SetSpecialAmmo(TacticalItemType.Hunter_Piercing);
            Debug.Log("<color=magenta>[전술 무기]: ★ 철갑 관통탄(레일건 빔) 장전 완료! 엄폐물 무시 저격 가능!</color>");
        }
    }
}