using UnityEngine;

/// <summary>
/// [Animation Part] CS0111 중복 에러를 유발하던 구형 1인칭 투명화 함수를 완벽히 걷어내고,
/// 히더의 게걸음, 대시 구르기, 사망 모션을 Animator 파라미터로 실시간 전달하는 독립 모듈입니다.
/// </summary>
public partial class PlayerController
{
    private Animator _cachedAnimator;

    private void UpdateAnimationState()
    {
        if (_cachedAnimator == null)
        {
            _cachedAnimator = GetComponentInChildren<Animator>();
        }

        if (_cachedAnimator == null) return;

        // 1. 숨는 자(Hider)의 좌우 게걸음 이동 파라미터 전달 (-1 ~ +1)
        if (CurrentRole == Role.Hider && !IsHit)
        {
            // _currentHiderVelocity 수치를 Animator의 MoveX로 주입
            _cachedAnimator.SetFloat("MoveX", _currentHiderVelocity);
        }
        else
        {
            _cachedAnimator.SetFloat("MoveX", 0f);
        }
    }

    // 대시 구르기 애니메이션 격발
    public void TriggerRollAnimation()
    {
        if (_cachedAnimator != null) _cachedAnimator.SetTrigger("Roll");
    }

    // 사망 피격 애니메이션 격발
    public void TriggerDieAnimation()
    {
        if (_cachedAnimator != null) _cachedAnimator.SetTrigger("Die");
    }
}