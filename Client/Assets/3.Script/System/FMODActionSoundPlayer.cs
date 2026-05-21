using UnityEngine;
using FMODUnity;

public class FMODActionSoundPlayer : MonoBehaviour
{
    public static FMODActionSoundPlayer Instance { get; private set; }

    [Header("Action Sounds")]
    [Tooltip("내 턴/캐릭터가 공격할 때 나는 소리")]
    public EventReference myAttackSound;
    [Tooltip("다른 파티원/몬스터가 공격할 때 나는 소리")]
    public EventReference otherAttackSound;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 공격 모션이 재생될 때 이 메서드를 호출합니다.
    /// </summary>
    public void PlayAttackSound(bool isMine)
    {
        EventReference targetSound = isMine ? myAttackSound : otherAttackSound;
        if (targetSound.IsNull) return;

        PlayInstance(targetSound, 1.0f, "Attack Sound");
    }

    /// <summary>
    /// SoundAction에서 호출: FMOD Event Path로 범용 사운드를 재생합니다.
    /// path 예: "event:/SFX/Attack/Sword"
    /// </summary>
    public void PlayByEventPath(string fmodEventPath, float volume = 1.0f)
    {
        if (string.IsNullOrEmpty(fmodEventPath)) return;

        PlayInstance(fmodEventPath, volume, fmodEventPath);
    }

    private static void PlayInstance(EventReference eventReference, float volume, string logContext)
    {
        if (eventReference.IsNull) return;
        try
        {
            PlayCreatedInstance(RuntimeManager.CreateInstance(eventReference), volume);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Sound create failed for '{logContext}': {e.Message}");
        }
    }

    private static void PlayInstance(string eventPath, float volume, string logContext)
    {
        try
        {
            PlayCreatedInstance(RuntimeManager.CreateInstance(eventPath), volume);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Sound create failed for '{logContext}': {e.Message}");
        }
    }

    private static void PlayCreatedInstance(FMOD.Studio.EventInstance instance, float volume)
    {
        int currentPitchOffset = 0;
        if (FMODDrumSequencer.Instance != null)
            currentPitchOffset = FMODDrumSequencer.Instance.GetCurrentPitchOffset();

        instance.setParameterByName("PitchOffset", currentPitchOffset);
        instance.setVolume(volume);
        instance.start();
        instance.release();
    }
}
