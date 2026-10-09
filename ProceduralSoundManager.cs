using UnityEngine;

/// <summary>
/// [오더 100% 반영]: 하드코딩된 사운드를 모두 제거하고, 
/// 사용자가 유니티 에디터 인스펙터에서 직접 오디오 클립을 무제한으로 갈아 끼우며
/// 완벽한 타격감을 찾을 수 있도록 전면 개방된 사운드 매니저입니다.
/// </summary>
public class ProceduralSoundManager : MonoBehaviour
{
    public static ProceduralSoundManager Instance { get; private set; }

    private AudioSource _audioSource;

    [Header("사격 사운드 (여러 개 넣으면 쏠 때마다 랜덤 재생)")]
    public AudioClip[] RifleShootSounds;
    public AudioClip[] BazookaShootSounds;
    public AudioClip[] SniperShootSounds;
    public AudioClip[] DryFireSounds;

    [Header("타격 및 파괴 사운드")]
    public AudioClip[] HitSounds;
    public AudioClip[] BlockBreakSounds;

    [Header("아이템 및 시스템 사운드")]
    public AudioClip[] StarCollectSounds;
    public AudioClip[] ReloadSounds;
    public AudioClip[] EquipBazookaSounds;
    public AudioClip[] VictoryFanfareSounds;

    private void Awake()
    {
        // 씬이 넘어가도 소리가 끊기지 않도록 영구 보존 처리
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        }
        else 
        { 
            Destroy(gameObject); 
            return; 
        }

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    /// <summary>
    /// 배열에 넣은 사운드 중 하나를 무작위로 골라 재생하여 단조로움을 없앱니다.
    /// </summary>
    private void PlayRandomClip(AudioClip[] clips, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) 
        {
            _audioSource.PlayOneShot(clip, volume);
        }
    }

    // 전투 스크립트(Combat.cs)에서 호출되는 직통 연결 함수들
    public void PlayShootSound() => PlayRandomClip(RifleShootSounds, 0.7f);
    public void PlayBazookaFireSound() => PlayRandomClip(BazookaShootSounds, 1.2f);
    public void PlaySniperFireSound() => PlayRandomClip(SniperShootSounds, 1.2f);
    public void PlayDryFireSound() => PlayRandomClip(DryFireSounds, 0.8f);
    
    // 벽에 맞거나, 상자를 밀거나, 탄피가 떨어질 때 나는 소리
    public void PlayHitSound() => PlayRandomClip(HitSounds, 0.9f);
    
    // 벽돌이 완전히 부서질 때 나는 와장창 소리
    public void PlayBlockBreakSound() => PlayRandomClip(BlockBreakSounds, 1.0f);
    
    public void PlayStarCollectSound() => PlayRandomClip(StarCollectSounds, 1.0f);
    public void PlayReloadSound() => PlayRandomClip(ReloadSounds, 1.0f);
    public void PlayBazookaEquipSound() => PlayRandomClip(EquipBazookaSounds, 1.0f);
    public void PlayVictoryFanfareSound() => PlayRandomClip(VictoryFanfareSounds, 1.0f);
}