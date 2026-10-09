using UnityEngine;
using Fusion;
using System.Collections;

/// <summary>
/// [Hider Skills Part] 라운드 종료(사망/승리) 시 입력을 원천 차단하여
/// 모션이 강제로 풀리는 현상을 방지하고, 좌우 롤링 회피 및 네트워크 동기화를 총괄합니다.
/// </summary>
public partial class PlayerController : NetworkBehaviour
{
    [Header("히더 롤링 설정")]
    [SerializeField] private float _rollDistance = 3.5f;   // 롤링 이동 거리
    [SerializeField] private float _rollDuration = 0.28f;  // 롤링 소요 시간
    [SerializeField] private float _rollCooldown = 1.0f;   // 롤링 쿨타임

    private float _lastRollTime = -999f;
    private bool _isRolling = false;
    private bool _isActionLocked = false; // ★ 사망 및 라운드 종료 시 입력 잠금 플래그

    public void SetActionLock(bool locked)
    {
        _isActionLocked = locked;
        if (locked) _isRolling = false;
    }

    public void CheckHiderDashInput()
    {
        if (CurrentRole != Role.Hider || !IsLocalControlled) return;
        if (_isRolling || _isActionLocked || IsHit) return; // ★ 피격되거나 라운드 종료 시 시프트 입력 완벽 차단

        float h = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            float targetDir = (Mathf.Abs(h) > 0.1f) ? Mathf.Sign(h) : -1.0f;
            TryExecuteRoll(targetDir);
        }
    }

    public void OnMobileRollButtonClicked()
    {
        if (CurrentRole != Role.Hider || !IsLocalControlled) return;
        if (_isRolling || _isActionLocked || IsHit) return;

        float h = Input.GetAxisRaw("Horizontal");
        float targetDir = (Mathf.Abs(h) > 0.1f) ? Mathf.Sign(h) : -1.0f;
        TryExecuteRoll(targetDir);
    }

    public void TryExecuteRoll(float direction)
    {
        if (_isActionLocked || IsHit) return;
        if (Time.time < _lastRollTime + _rollCooldown) return;
        _lastRollTime = Time.time;

        RPC_RequestRoll(direction);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRoll(float direction)
    {
        RPC_BroadcastRoll(direction);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastRoll(float direction)
    {
        StartCoroutine(NetworkRollRoutine(direction));
    }

    private IEnumerator NetworkRollRoutine(float direction)
    {
        _isRolling = true;

        if (IsLocalControlled)
        {
            TriggerHapticFeedback();
        }

        if (ProceduralSoundManager.Instance != null)
        {
            ProceduralSoundManager.Instance.PlayShootSound();
        }

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            if (direction > 0f)
            {
                anim.SetTrigger("RollRight");
            }
            else
            {
                anim.SetTrigger("Roll");
            }
        }

        float elapsed = 0f;
        float startX = transform.position.x;
        float targetX = Mathf.Clamp(startX + (direction * _rollDistance), -10.0f, 10.0f);

        while (elapsed < _rollDuration)
        {
            if (_isActionLocked || IsHit) break; // 도중 피격 시 즉시 탈출

            elapsed += Time.deltaTime;
            float t = elapsed / _rollDuration;

            float currentX = Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, t));
            transform.position = new Vector3(currentX, 1.0f, 15.0f);
            HiderPosX = currentX;

            yield return null;
        }

        if (!_isActionLocked && !IsHit)
        {
            transform.position = new Vector3(targetX, 1.0f, 15.0f);
            HiderPosX = targetX;
        }

        _isRolling = false;
    }
}