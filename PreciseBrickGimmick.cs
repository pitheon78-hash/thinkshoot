using UnityEngine;

public class PreciseBrickGimmick : MonoBehaviour
{
    public GameObject FakeDebrisPrefab;

    // ★ 핵심: P1과 P2의 파괴 상태를 완벽히 독립적으로 기억
    private bool _isBrokenByP1 = false;
    private bool _isBrokenByP2 = false;

    public void BreakBrick(Vector3 hitDirection)
    {
        // 현재 총을 쏜 사냥꾼이 누구인지 확인
        int currentHunter = (GameLoopManager.Instance != null) ? GameLoopManager.Instance.CurrentHunterIndex : 0;
        
        // 이미 이 사냥꾼 화면에서 부서진 상태면 무시
        bool isAlreadyBroken = (currentHunter == 0) ? _isBrokenByP1 : _isBrokenByP2;
        if (isAlreadyBroken) return;

        // 파괴 상태 영구 기록
        if (currentHunter == 0) _isBrokenByP1 = true;
        else _isBrokenByP2 = true;

        // 1. 가짜 물리 파편 흩뿌리기
        if (FakeDebrisPrefab != null)
        {
            GameObject debris = Instantiate(FakeDebrisPrefab, transform.position, transform.rotation);
            Rigidbody rb = debris.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(hitDirection * 5f, ForceMode.Impulse);
        }

        // 2. 화면에서 구멍 뚫기
        ApplyVisibility(false);
    }

    // ★ 턴 교대 시, 사냥꾼 번호에 맞는 벽 상태만 쏙 빼서 불러옴
    public void LoadWallStateForHunter(int hunterIndex)
    {
        bool isBroken = (hunterIndex == 0) ? _isBrokenByP1 : _isBrokenByP2;
        ApplyVisibility(!isBroken); // 안 부서졌으면 보이게, 부서졌으면 안 보이게!
    }

    // 새로운 세트(1턴) 시작 시 완전 새것으로 청소
    public void HardResetAll()
    {
        _isBrokenByP1 = false;
        _isBrokenByP2 = false;
        ApplyVisibility(true);
    }

    private void ApplyVisibility(bool isVisible)
    {
        MeshRenderer mesh = GetComponent<MeshRenderer>();
        if (mesh != null) mesh.enabled = isVisible;
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = isVisible;
    }
}