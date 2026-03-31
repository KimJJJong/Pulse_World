using UnityEngine;
using FMODUnity;
using Shared.Data;

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

        // FMOD Event 인스턴스 생성
        FMOD.Studio.EventInstance instance;
        try
        {
            instance = RuntimeManager.CreateInstance(targetSound);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Attack Sound Create Failed: {e.Message}");
            return;
        }

        // 현재 재생 중인 음악(BGM)의 리듬/화성 정보를 가져옴
        int currentPitchOffset = 0;
        if (FMODDrumSequencer.Instance != null)
        {
            currentPitchOffset = FMODDrumSequencer.Instance.GetCurrentPitchOffset();
        }

        // FMOD 파라미터 적용 (FMOD Studio에서 'PitchOffset' 파라미터가 만들어져 있어야 함!)
        instance.setParameterByName("PitchOffset", currentPitchOffset);

        // 즉시 재생 후 메모리 반환 처리
        instance.start();
        instance.release();

        string logStr = isMine ? "My Attack" : "Other Attack";
        // Debug.Log($"[FMODAction] Played {logStr} with PitchOffset: {currentPitchOffset}");
    }
}
