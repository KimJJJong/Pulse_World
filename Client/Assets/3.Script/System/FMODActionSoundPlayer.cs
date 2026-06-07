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

        string weaponType = "";
        if (fmodEventPath.IndexOf("dagger", System.StringComparison.OrdinalIgnoreCase) >= 0) weaponType = "Dagger";
        else if (fmodEventPath.IndexOf("greatsword", System.StringComparison.OrdinalIgnoreCase) >= 0) weaponType = "Greatsword";
        else if (fmodEventPath.IndexOf("bow", System.StringComparison.OrdinalIgnoreCase) >= 0) weaponType = "Bow";
        else if (fmodEventPath.IndexOf("parry", System.StringComparison.OrdinalIgnoreCase) >= 0) weaponType = "Parry";
        else if (fmodEventPath.IndexOf("staff", System.StringComparison.OrdinalIgnoreCase) >= 0) weaponType = "Staff";

        if (!string.IsNullOrEmpty(weaponType))
        {
            string genre = "Synthwave";
            if (FMODDrumSequencer.Instance != null)
                genre = FMODDrumSequencer.Instance.GetCurrentGenre();

            string resolvedEventPath = $"event:/{genre}_{weaponType}";

            if (weaponType == "Staff")
            {
                Shared.Data.ChordEvent chord = null;
                if (FMODDrumSequencer.Instance != null)
                    chord = FMODDrumSequencer.Instance.GetCurrentChord();

                StartCoroutine(PlayStaffArpeggio(resolvedEventPath, volume, chord));
            }
            else
            {
                PlayInstance(resolvedEventPath, volume, resolvedEventPath);
            }
            return;
        }

        PlayInstance(fmodEventPath, volume, fmodEventPath);
    }

    private static void PlayInstance(EventReference eventReference, float volume, string logContext)
    {
        if (eventReference.IsNull) return;
        try
        {
            string path = string.Empty;
#if UNITY_EDITOR
            var eventInfo = FMODUnity.EventManager.EventFromGUID(eventReference.Guid);
            if (eventInfo != null) path = eventInfo.Path;
#endif
            PlayCreatedInstance(RuntimeManager.CreateInstance(eventReference), volume, path);
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
            PlayCreatedInstance(RuntimeManager.CreateInstance(eventPath), volume, eventPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Sound create failed for '{logContext}': {e.Message}");
        }
    }

    private static void PlayCreatedInstance(FMOD.Studio.EventInstance instance, float volume, string eventPath)
    {
        int finalPitchOffset = 0;
        if (FMODDrumSequencer.Instance != null)
        {
            Shared.Data.ChordEvent chord = FMODDrumSequencer.Instance.GetCurrentChord();
            finalPitchOffset = GetHarmonicPitchOffset(chord, eventPath);
        }

        instance.setParameterByName("PitchOffset", finalPitchOffset);
        instance.setVolume(volume);
        instance.start();
        instance.release();
    }

    public static int GetHarmonicPitchOffset(Shared.Data.ChordEvent chord, string path)
    {
        if (chord == null) return 0;
        
        int rootOffset = chord.PitchOffset;
        string chordType = chord.ChordType.ToLowerInvariant();
        
        int degree = 1;
        int octaveOffset = 0;
        
        if (string.IsNullOrEmpty(path))
        {
            return rootOffset;
        }

        // 무기 분류 파싱
        if (path.IndexOf("dagger", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            degree = 3;
            octaveOffset = 1; // 3도 고음역
        }
        else if (path.IndexOf("greatsword", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            degree = 1;
            octaveOffset = -1; // 1도 저음역
        }
        else if (path.IndexOf("bow", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            degree = 7;
            octaveOffset = 1; // 7도 고음역
        }
        else if (path.IndexOf("parry", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            degree = 1;
            octaveOffset = 2; // 1도 초고음역
        }
        
        int degreeOffset = 0;
        if (degree == 3)
        {
            degreeOffset = IsMinorLike(chordType) ? 3 : 4;
        }
        else if (degree == 5)
        {
            degreeOffset = chordType.Contains("dim") || chordType.Contains("halfdiminished") ? 6 : chordType.Contains("aug") ? 8 : 7;
        }
        else if (degree == 7)
        {
            degreeOffset = chordType.Contains("maj7") || chordType.Contains("major7") ? 11 : 10;
        }
        
        return rootOffset + degreeOffset + (octaveOffset * 12);
    }
    
    private static bool IsMinorLike(string chordType)
    {
        return chordType.Contains("minor") ||
               chordType.Contains("min") ||
               chordType.Contains("dim") ||
               chordType.Contains("halfdiminished");
    }

    private System.Collections.IEnumerator PlayStaffArpeggio(string fmodEventPath, float volume, Shared.Data.ChordEvent chord)
    {
        int[] degrees = { 1, 3, 5, 1 };
        int[] octaves = { 0, 0, 0, 1 };
        
        for (int i = 0; i < degrees.Length; i++)
        {
            int deg = degrees[i];
            int oct = octaves[i];
            
            int rootOffset = chord != null ? chord.PitchOffset : 0;
            string chordType = chord != null ? chord.ChordType.ToLowerInvariant() : "major";
            
            int degreeOffset = 0;
            if (deg == 3)
            {
                degreeOffset = IsMinorLike(chordType) ? 3 : 4;
            }
            else if (deg == 5)
            {
                degreeOffset = chordType.Contains("dim") || chordType.Contains("halfdiminished") ? 6 : chordType.Contains("aug") ? 8 : 7;
            }
            
            int finalOffset = rootOffset + degreeOffset + (oct * 12);
            
            try
            {
                var instance = RuntimeManager.CreateInstance(fmodEventPath);
                instance.setParameterByName("PitchOffset", finalOffset);
                instance.setVolume(volume * 0.5f); // 겹침으로 인한 볼륨 절반 보정
                instance.start();
                instance.release();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FMODAction] Staff arpeggio node create failed: {e.Message}");
            }
            
            yield return new WaitForSeconds(0.05f);
        }
    }
}
