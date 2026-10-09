using UnityEngine;

/// <summary>
/// [Mobile Look Part] CS0103 에러를 유발하던 1인칭 구형 무기 스왑 찌꺼기를 영구 절제하고,
/// 순수하게 모바일 화면 터치 드래그로 사냥꾼의 카메라 시점을 100% 부드럽게 회전시키는 최적화 모듈입니다.
/// </summary>
public partial class PlayerController
{
    public void ApplyMobileCameraLook(float deltaX, float deltaY)
    {
        if (!IsLocalControlled || CurrentRole != Role.Hunter) return;

        // 좌우(Yaw) 및 상하(Pitch) 회전값 누적 및 앵글 제한
        _yaw = Mathf.Clamp(_yaw + deltaX, -35f, 35f);
        _pitch = Mathf.Clamp(_pitch - deltaY, -20f, 20f);

        // 3인칭 숄더뷰를 통제하는 전용 카메라 각도 즉각 갱신
        if (_localCamera != null)
        {
            _localCamera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }

    public void SetMobileMoveAxis(float axis)
    {
        _mobileMoveAxisX = axis;
    }

    public void SetSpecialAmmo(TacticalItemType ammoType)
    {
        // 시각적 무기 스왑은 이미 Visual.cs에서 완벽하게 처리하므로 상태값만 가볍게 갱신!
        CurrentSpecialAmmo = ammoType;
        ApplyLaserVisualUpdate(ammoType);
    }
}