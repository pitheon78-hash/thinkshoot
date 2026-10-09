using UnityEngine;
using System.Collections;

/// <summary>
/// [기믹 09] 팽그르르 비틀림 물리 연출과 양방향 완벽 싱크를 결합한 전술 헤비박스입니다.
/// 사격 및 폭발 충격 시 묵직하게 뒤로 밀려나며 모서리가 비틀려 새로운 엄폐 틈새를 만듭니다.
/// </summary>
public class HeavyBoxGimmick : MonoBehaviour
{
    [Header("헤비박스 식별 번호 (0 또는 1)")]
    public int BoxId = 0;

    [Header("물리 비틀림 및 밀림 계수")]
    [SerializeField] private float _pushDistance = 1.3f;   // 1회 피격 시 후퇴 거리
    [SerializeField] private float _slideDuration = 0.24f;  // 물리 감속 시간

    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _targetPos;
    private Quaternion _targetRot;
    private bool _isSliding = false;

    private void Awake()
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
        _targetPos = _startPos;
        _targetRot = _startRot;
    }

    /// <summary>
    /// PlayerController.Combat에서 호출하는 메인 피격 함수 (CS1061 완벽 해결)
    /// </summary>
    public void PushBox(Vector3 hitDirection)
    {
        // 로컬 즉각 반응 계산
        Vector3 nextPos = CalculateNextPosition(hitDirection);
        float spinY = Random.Range(25f, 45f) * (Random.value > 0.5f ? 1f : -1f);
        ApplySyncedPush(nextPos, spinY);
    }

    public Vector3 CalculateNextPosition(Vector3 hitDirection)
    {
        Vector3 dir = hitDirection.normalized;
        Vector3 next = _targetPos + new Vector3(dir.x, 0f, dir.z) * _pushDistance;
        next.x = Mathf.Clamp(next.x, -9.0f, 9.0f);
        next.z = Mathf.Clamp(next.z, -5.0f, 14.0f);
        return next;
    }

    /// <summary>
    /// 서버 브로드캐스트를 받아 양쪽 기기에서 정확히 동일하게 비틀며 슬라이딩
    /// </summary>
    public void ApplySyncedPush(Vector3 targetPos, float spinAngleY)
    {
        _targetPos = targetPos;
        _targetRot = _targetRot * Quaternion.Euler(0f, spinAngleY, 0f);

        if (_isSliding) StopAllCoroutines();
        StartCoroutine(PhysicsSpinSlideRoutine());
    }

    private IEnumerator PhysicsSpinSlideRoutine()
    {
        _isSliding = true;
        if (ProceduralSoundManager.Instance != null) ProceduralSoundManager.Instance.PlayHitSound();

        Vector3 initPos = transform.position;
        Quaternion initRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < _slideDuration)
        {
            float t = elapsed / _slideDuration;
            // 500kg 묵직한 물체의 감속 마찰 커브 (Cubic Ease Out)
            float smooth = 1f - Mathf.Pow(1f - t, 3f);

            transform.position = Vector3.Lerp(initPos, _targetPos, smooth);
            transform.rotation = Quaternion.Slerp(initRot, _targetRot, smooth);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = _targetPos;
        transform.rotation = _targetRot;
        _isSliding = false;
    }

    public void ResetGimmick()
    {
        StopAllCoroutines();
        _isSliding = false;
        transform.position = _startPos;
        transform.rotation = _startRot;
        _targetPos = _startPos;
        _targetRot = _startRot;
    }
}