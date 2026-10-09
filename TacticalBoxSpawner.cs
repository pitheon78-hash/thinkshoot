using UnityEngine;
using Fusion;

/// <summary>
/// 매 라운드(턴)마다 30% 확률로 상공 보급 상자를 띄우는 서버 전용 스폰 매니저입니다.
/// </summary>
public class TacticalBoxSpawner : NetworkBehaviour
{
    public static TacticalBoxSpawner Instance { get; private set; }

    [Header("스폰 설정")]
    [SerializeField, Tooltip("프로젝트 창의 Tactical_FloatingBox 프리팹 연결")] 
    private NetworkPrefabRef _boxPrefab;
    
    [SerializeField, Range(0, 100)] 
    private int _spawnProbability = 30; // 30% 확률
    
    [SerializeField, Tooltip("박스가 나타날 수 있는 3개의 공중 좌표 (좌, 중, 우)")] 
    private Vector3[] _spawnPoints;

    private NetworkObject _currentBox;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 라운드(턴) 시작 시 GameLoopManager에서 이 함수를 단 1번 호출해야 합니다.
    /// </summary>
    public void TrySpawnBoxForNewTurn()
    {
        // 오직 서버(방장)만 주사위를 굴릴 권한이 있습니다. (중복 스폰 방지)
        if (!HasStateAuthority) return;

        // 1. 이전 라운드에 남은 상자가 있다면 청소 (Despawn)
        if (_currentBox != null)
        {
            Runner.Despawn(_currentBox);
            _currentBox = null;
        }

        // 2. 30% 확률 주사위 굴리기
        int roll = Random.Range(1, 101);
        if (roll <= _spawnProbability)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return;

            // 랜덤 위치 선정 및 스폰!
            Vector3 spawnPos = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            _currentBox = Runner.Spawn(_boxPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[Tactical] 주사위 {roll} - 30% 당첨! 보급 상자 스폰 완료!");
        }
        else
        {
            Debug.Log($"[Tactical] 주사위 {roll} - 꽝. 이번 턴에는 보급 상자가 없습니다.");
        }
    }
}