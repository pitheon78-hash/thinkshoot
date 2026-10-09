using UnityEngine;

public class GravityBrickWall : MonoBehaviour
{
    [Tooltip("조각조각 나뉘어 Rigidbody가 달린 벽돌 파편 프리팹")]
    public GameObject FracturedWallPrefab;
    
    [Tooltip("(선택사항) 내 바로 위에 얹혀있는 가짜 통짜 벽 덩어리 연결 (밑돌 빼기 연쇄 붕괴용)")]
    public GravityBrickWall LinkedUpperWall;
    
    private bool _isDestroyed = false;

    public void CollapseWall(Vector3 hitDirection)
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        // 1. 파편 덩어리 생성
        if (FracturedWallPrefab != null)
        {
            GameObject debris = Instantiate(FracturedWallPrefab, transform.position, transform.rotation);
            Rigidbody[] bricks = debris.GetComponentsInChildren<Rigidbody>();
            foreach (var brick in bricks)
            {
                brick.AddForce(hitDirection * Random.Range(3f, 8f), ForceMode.Impulse);
            }
        }

        // 2. 가짜 통짜 벽 가리기 (충돌체 제거)
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers) r.enabled = false;
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;

        // 3. [핵심 추가] 내 위에 얹혀있는 윗돌이 있다면? 네트워크 패킷 없이 로컬에서 알아서 무너뜨림!
        if (LinkedUpperWall != null && !LinkedUpperWall._isDestroyed)
        {
            // 윗돌은 중력에 의해 아래로 무너지는 것처럼 방향(Vector3.down)을 줌
            LinkedUpperWall.CollapseWall(Vector3.down * 1.5f);
        }
    }
}