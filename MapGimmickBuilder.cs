using UnityEngine;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// [Map Gimmick Builder] ILWeaver 컴파일 에러의 진짜 원인이었던 [Rpc] 함수 이름 규격(RPC_ 접두사)을 완벽히 교정하고,
/// 수동 배치 맵의 물리(PhysX) 권한을 1라운드부터 클라이언트에게 100% 동기화시키는 마스터 맵 매니저입니다.
/// </summary>
public class MapGimmickBuilder : NetworkBehaviour
{
    public static MapGimmickBuilder Instance { get; private set; }

    public enum GimmickType
    {
        DestructibleBlock,
        ExplosiveBarrel,
        StarItem
    }

    [Header("엄폐물 붕괴 머티리얼")]
    [SerializeField] private Material _brickMaterial;
    [SerializeField] private Material _reinforcedMaterial;
    [SerializeField] private Material _barrelMaterial;

    private List<PhysXBrickWall> _spawnedBricks = new List<PhysXBrickWall>();
    private List<GameObject> _gimmickObjects = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        BuildTacticalMap();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            RPC_ForceRebuildMap();
        }
    }

    /// <summary>
    /// ★ [에러 진범 완벽 박멸]: 함수 이름을 퓨전 규격에 맞게 'RPC_' 로 시작하도록 수정!
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ForceRebuildMap()
    {
        foreach (var brick in _spawnedBricks)
        {
            if (brick != null) brick.ResetBrick();
        }

        foreach (var obj in _gimmickObjects)
        {
            if (obj != null) obj.SetActive(true);
        }
        
        Debug.Log("<color=green>[MapBuilder] 전체 맵 물리 엔진 동기화 리셋 완료! 1턴부터 완벽 붕괴 적용.</color>");
    }

    public void BuildTacticalMap()
    {
        PhysXBrickWall[] manualBricks = FindObjectsOfType<PhysXBrickWall>();
        
        if (manualBricks.Length > 0)
        {
            Debug.Log($"<color=green>[MapBuilder] 수동으로 배치된 커스텀 맵 감지! (총 {manualBricks.Length}개 블록). 자동 맵 생성을 스킵합니다.</color>");
            
            _spawnedBricks.Clear();
            _gimmickObjects.Clear();
            
            _spawnedBricks.AddRange(manualBricks);

            GimmickTarget[] gimmicks = FindObjectsOfType<GimmickTarget>();
            foreach(var g in gimmicks)
            {
                if (g.TargetType == GimmickType.ExplosiveBarrel) 
                {
                    _gimmickObjects.Add(g.gameObject);
                }
            }
            return; 
        }

        foreach (var b in _spawnedBricks) if (b != null) Destroy(b.gameObject);
        _spawnedBricks.Clear();

        foreach (var g in _gimmickObjects) if (g != null) Destroy(g);
        _gimmickObjects.Clear();

        Shader safeShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (_brickMaterial == null) _brickMaterial = new Material(safeShader) { color = new Color(0.82f, 0.75f, 0.65f) };
        if (_barrelMaterial == null) _barrelMaterial = new Material(safeShader) { color = new Color(0.9f, 0.2f, 0.15f) };
        if (_reinforcedMaterial == null) _reinforcedMaterial = new Material(safeShader) { color = new Color(0.55f, 0.58f, 0.62f) };

        BuildWallSection(new Vector3(-5.2f, 0.5f, 11.0f), 5, 4, false);
        BuildCenterBunker(new Vector3(0.0f, 0.5f, 11.5f));
        BuildWallSection(new Vector3(4.8f, 0.5f, 11.0f), 4, 4, true);
    }

    private void BuildWallSection(Vector3 basePos, int width, int height, bool isStairs)
    {
        float blockW = 1.05f, blockH = 0.58f, blockD = 0.65f;
        float startX = basePos.x - ((width - 1) * blockW * 0.5f);

        for (int y = 0; y < height; y++)
        {
            int currentWidth = isStairs ? (width - (y / 2)) : width;
            for (int x = 0; x < currentWidth; x++)
            {
                Vector3 pos = new Vector3(startX + (x * blockW), basePos.y + (y * blockH), basePos.z);
                CreateSinglePhysXBrick(pos, new Vector3(blockW * 0.96f, blockH * 0.96f, blockD), _brickMaterial);
            }
        }
    }

    private void BuildCenterBunker(Vector3 centerPos)
    {
        float blockW = 1.05f, blockH = 0.58f, blockD = 0.65f;

        GameObject heavyCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        heavyCover.name = "Center_HeavyCover";
        heavyCover.transform.SetParent(this.transform);
        heavyCover.transform.position = centerPos + new Vector3(0, 0.3f, -1.2f);
        heavyCover.transform.localScale = new Vector3(3.2f, 1.4f, 0.8f);
        heavyCover.GetComponent<Renderer>().material = _reinforcedMaterial;
        _gimmickObjects.Add(heavyCover);

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Explosive_Barrel";
        barrel.transform.SetParent(this.transform);
        barrel.transform.position = centerPos + new Vector3(0, 1.3f, 0f);
        barrel.transform.localScale = new Vector3(0.9f, 0.8f, 0.9f);
        barrel.GetComponent<Renderer>().material = _barrelMaterial;

        GimmickTarget gtBarrel = barrel.AddComponent<GimmickTarget>();
        gtBarrel.Init(9999, GimmickType.ExplosiveBarrel);
        _gimmickObjects.Add(barrel);

        for (int y = 0; y < 5; y++)
        {
            CreateSinglePhysXBrick(centerPos + new Vector3(-1.6f, y * blockH, 0), new Vector3(blockW * 0.96f, blockH * 0.96f, blockD), _brickMaterial);
            CreateSinglePhysXBrick(centerPos + new Vector3(1.6f, y * blockH, 0), new Vector3(blockW * 0.96f, blockH * 0.96f, blockD), _brickMaterial);
            if (y >= 3) CreateSinglePhysXBrick(centerPos + new Vector3(0f, y * blockH, 0), new Vector3(blockW * 1.8f, blockH * 0.96f, blockD), _brickMaterial);
        }
    }

    private void CreateSinglePhysXBrick(Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject brickObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brickObj.name = $"PhysX_Brick_{_spawnedBricks.Count}";
        brickObj.transform.SetParent(this.transform);
        brickObj.transform.position = pos;
        brickObj.transform.localScale = scale;

        Renderer r = brickObj.GetComponent<Renderer>();
        if (r != null) r.material = mat;

        PhysXBrickWall brickComp = brickObj.AddComponent<PhysXBrickWall>();
        _spawnedBricks.Add(brickComp);

        GimmickTarget gt = brickObj.AddComponent<GimmickTarget>();
        gt.Init(_spawnedBricks.Count, GimmickType.DestructibleBlock);
    }

    public List<int> TriggerBarrelExplosion(Vector3 explosionPos, float radius)
    {
        List<int> affected = new List<int>();
        Collider[] hits = Physics.OverlapSphere(explosionPos, radius * 1.8f);
        foreach (var h in hits)
        {
            PhysXBrickWall brick = h.GetComponent<PhysXBrickWall>();
            if (brick != null) brick.AwakenPhysics(explosionPos, Vector3.up, 12.0f);
        }
        return affected;
    }
}