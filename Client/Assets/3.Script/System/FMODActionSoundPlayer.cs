using UnityEngine;
using FMODUnity;

public class FMODActionSoundPlayer : MonoBehaviour
{
    public static FMODActionSoundPlayer Instance { get; private set; }

    private static readonly System.Collections.Generic.Dictionary<string, FMOD.Sound> _localSoundCache = new System.Collections.Generic.Dictionary<string, FMOD.Sound>();

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
    public void PlayAttackSound(bool isMine, float startOffsetMs = 0f)
    {
        EventReference targetSound = isMine ? myAttackSound : otherAttackSound;
        if (targetSound.IsNull) return;

        PlayInstance(targetSound, 1.0f, "Attack Sound", startOffsetMs);
    }

    /// <summary>
    /// SoundAction에서 호출: FMOD Event Path로 범용 사운드를 재생합니다.
    /// path 예: "event:/SFX/Attack/Sword"
    /// </summary>
    public void PlayByEventPath(string fmodEventPath, float volume = 1.0f, float startOffsetMs = 0f)
    {
        if (string.IsNullOrWhiteSpace(fmodEventPath)) return;

        string requestedPath = NormalizeKnownEventPath(fmodEventPath.Trim());

        if (requestedPath.StartsWith("Forest_", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayLocalWavSound(requestedPath, volume, startOffsetMs);
            return;
        }

        string weaponType = ResolveWeaponType(requestedPath);

        if (requestedPath.StartsWith("event:/", System.StringComparison.OrdinalIgnoreCase))
        {
            if (weaponType == "Staff")
            {
                Shared.Data.ChordEvent chord = null;
                if (FMODDrumSequencer.Instance != null)
                    chord = FMODDrumSequencer.Instance.GetCurrentChord();

                StartCoroutine(PlayStaffArpeggio(requestedPath, volume, chord));
            }
            else
            {
                PlayInstance(requestedPath, volume, requestedPath, startOffsetMs);
            }
            return;
        }

        if (!string.IsNullOrEmpty(weaponType))
        {
            string genre = "Synthwave";
            if (FMODDrumSequencer.Instance != null)
                genre = FMODDrumSequencer.Instance.GetCurrentGenre();

            string resolvedEventPath = $"event:/SFX/{genre}_{weaponType}";

            if (weaponType == "Staff")
            {
                Shared.Data.ChordEvent chord = null;
                if (FMODDrumSequencer.Instance != null)
                    chord = FMODDrumSequencer.Instance.GetCurrentChord();

                StartCoroutine(PlayStaffArpeggio(resolvedEventPath, volume, chord));
            }
            else
            {
                PlayInstance(resolvedEventPath, volume, resolvedEventPath, startOffsetMs);
            }
            return;
        }

        PlayInstance(requestedPath, volume, requestedPath, startOffsetMs);
    }

    private static string ResolveWeaponType(string pathOrKey)
    {
        if (pathOrKey.IndexOf("dagger", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Dagger";
        if (pathOrKey.IndexOf("greatsword", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Greatsword";
        if (pathOrKey.IndexOf("bow", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Bow";
        if (pathOrKey.IndexOf("parry", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Parry";
        if (pathOrKey.IndexOf("staff", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Staff";
        return string.Empty;
    }

    private static string NormalizeKnownEventPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string[] genres = { "Synthwave", "Lofi", "Funk", "Orchestral", "Jazz" };
        string[] weapons = { "Dagger", "Greatsword", "Bow", "Parry", "Staff" };

        foreach (string genre in genres)
        {
            foreach (string weapon in weapons)
            {
                string eventName = $"{genre}_{weapon}";
                if (path.Equals(eventName, System.StringComparison.OrdinalIgnoreCase) ||
                    path.Equals($"event:/{eventName}", System.StringComparison.OrdinalIgnoreCase) ||
                    path.Equals($"event:/SFX/{eventName}", System.StringComparison.OrdinalIgnoreCase))
                {
                    return $"event:/SFX/{eventName}";
                }
            }
        }

        return path;
    }

    private static void PlayInstance(EventReference eventReference, float volume, string logContext, float startOffsetMs = 0f)
    {
        if (eventReference.IsNull) return;
        try
        {
            string path = string.Empty;
#if UNITY_EDITOR
            var eventInfo = FMODUnity.EventManager.EventFromGUID(eventReference.Guid);
            if (eventInfo != null) path = eventInfo.Path;
#endif
            PlayCreatedInstance(RuntimeManager.CreateInstance(eventReference), volume, path, startOffsetMs);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Sound create failed for '{logContext}': {e.Message}");
        }
    }

    private static void PlayInstance(string eventPath, float volume, string logContext, float startOffsetMs = 0f)
    {
        if (eventPath.StartsWith("Forest_", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayLocalWavSound(eventPath, volume, startOffsetMs);
            return;
        }

        try
        {
            PlayCreatedInstance(RuntimeManager.CreateInstance(eventPath), volume, eventPath, startOffsetMs);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Sound create failed for '{logContext}': {e.Message}");
        }
    }

    private static void PlayCreatedInstance(FMOD.Studio.EventInstance instance, float volume, string eventPath, float startOffsetMs = 0f)
    {
        int finalPitchOffset = 0;
        if (FMODDrumSequencer.Instance != null)
        {
            Shared.Data.ChordEvent chord = FMODDrumSequencer.Instance.GetCurrentChord();
            finalPitchOffset = GetHarmonicPitchOffset(chord, eventPath);
        }

        instance.setParameterByName("PitchOffset", finalPitchOffset);
        ApplyStartOffset(instance, startOffsetMs);
        instance.setVolume(volume);
        instance.start();
        instance.release();
    }

    private static void ApplyStartOffset(FMOD.Studio.EventInstance instance, float startOffsetMs)
    {
        if (startOffsetMs <= 2f || startOffsetMs >= 300f)
            return;

        instance.setTimelinePosition(Mathf.RoundToInt(startOffsetMs));
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

    private void OnDestroy()
    {
        foreach (var sound in _localSoundCache.Values)
        {
            if (sound.hasHandle())
                sound.release();
        }
        _localSoundCache.Clear();
    }

    private static void PlayLocalWavSound(string sfxName, float volume, float startOffsetMs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sfxName)) return;

            FMOD.Sound sound;
            if (!_localSoundCache.TryGetValue(sfxName, out sound))
            {
                string wavPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Sound/Forest", $"{sfxName}.wav");
                if (!System.IO.File.Exists(wavPath))
                {
                    Debug.LogError($"[FMODAction] Local WAV not found at: {wavPath}");
                    return;
                }

                byte[] wavBytes = System.IO.File.ReadAllBytes(wavPath);
                var exInfo = new FMOD.CREATESOUNDEXINFO
                {
                    cbsize = System.Runtime.InteropServices.Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(),
                    length = (uint)wavBytes.Length
                };

                FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D | FMOD.MODE.LOOP_OFF;
                FMOD.RESULT createResult = RuntimeManager.CoreSystem.createSound(wavBytes, mode, ref exInfo, out sound);
                if (createResult != FMOD.RESULT.OK)
                {
                    Debug.LogError($"[FMODAction] Local sound create failed for '{sfxName}'. Result={createResult}");
                    return;
                }

                _localSoundCache[sfxName] = sound;
            }

            FMOD.RESULT playResult = RuntimeManager.CoreSystem.playSound(sound, default, true, out FMOD.Channel channel);
            if (playResult == FMOD.RESULT.OK)
            {
                float inGameVol = 1.0f;
                float master = PlayerPrefs.GetFloat("Options.MasterVolume", 0.85f);
                float ingame = PlayerPrefs.GetFloat("Options.InGameVolume", 0.80f);
                inGameVol = master * ingame;

                channel.setVolume(volume * inGameVol);

                if (startOffsetMs > 2f && startOffsetMs < 300f)
                {
                    channel.setPosition((uint)Mathf.RoundToInt(startOffsetMs), FMOD.TIMEUNIT.MS);
                }

                channel.setPaused(false);
            }
            else
            {
                Debug.LogError($"[FMODAction] Local sound play failed for '{sfxName}'. Result={playResult}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FMODAction] Local WAV play exception for '{sfxName}': {e.Message}");
        }
    }
}
