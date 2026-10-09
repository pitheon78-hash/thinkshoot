using UnityEngine;
using System.Collections;

/// <summary>
/// 씬에 배치된 8개의 파괴형 벽돌 스크립트입니다.
/// 고유 BlockId를 통해 네트워크 동기화 명령을 수신합니다.
/// </summary>
public class DestructibleBlock : MonoBehaviour
{
    [Header("블록 식별 번호 (0~7)")]
    public int BlockId = -1;

    private Vector3 _originPosition;
    private Quaternion _originRotation;
    private MeshRenderer _mainRenderer;
    private BoxCollider _mainCollider;
    private GameObject[] _debrisPieces;

    private void Awake()
    {
        _originPosition = transform.position;
        _originRotation = transform.rotation;
        _mainRenderer = GetComponent<MeshRenderer>();
        _mainCollider = GetComponent<BoxCollider>();

        // 오브젝트 이름 끝자리 숫자로 고유 ID 자동 부여 (WallBlock (1) -> 1)
        string name = gameObject.name;
        if (name.Contains("(") && name.Contains(")"))
        {
            int start = name.IndexOf("(") + 1;
            int length = name.IndexOf(")") - start;
            int.TryParse(name.Substring(start, length), out BlockId);
        }
        else
        {
            BlockId = 0; // WallBlock 기본형은 0번
        }
    }

    /// <summary>
    /// 네트워크 RPC를 통해 양쪽 기기 모두에서 시각적으로 파괴를 실행합니다.
    /// </summary>
    public void ExecuteDestruction(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (_mainCollider != null && !_mainCollider.enabled) return;

        if (_mainRenderer != null) _mainRenderer.enabled = false;
        if (_mainCollider != null) _mainCollider.enabled = false;

        CreateDebris(hitPoint, hitDirection);
    }

private void CreateDebris(Vector3 hitPoint, Vector3 hitDirection)
    {
        _debrisPieces = new GameObject[8];
        Vector3 size = transform.localScale / 2f;

        int index = 0;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    p.transform.position = transform.position + new Vector3(x * size.x * 0.25f, y * size.y * 0.25f, z * size.z * 0.25f);
                    p.transform.localScale = size * 0.5f;

                    Renderer r = p.GetComponent<Renderer>();
                    r.material.color = Color.magenta;

                    Rigidbody rb = p.AddComponent<Rigidbody>();
                    rb.mass = 0.5f;
                    rb.AddExplosionForce(350f, hitPoint, 5f);

                    // [추가]: 2.5초 뒤 파편 자동 소멸로 Hierarchy 찌꺼기 및 메모리 누수 방지
                    Destroy(p, 2.5f);

                    _debrisPieces[index++] = p;
                }
            }
        }
    }

    public void ResetBlock()
    {
        if (_debrisPieces != null)
        {
            foreach (var p in _debrisPieces)
            {
                if (p != null) Destroy(p);
            }
            _debrisPieces = null;
        }

        transform.position = _originPosition;
        transform.rotation = _originRotation;
        if (_mainRenderer != null) _mainRenderer.enabled = true;
        if (_mainCollider != null) _mainCollider.enabled = true;
    }
}