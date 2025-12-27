using UnityEngine;

public class RhythmClient : MonoBehaviour
{
    public static RhythmClient Instance { get; private set; }

    public double Bpm { get; private set; }
    public int BaseBeatDivision { get; private set; }
    public long ServerSongStartMs { get; private set; }

    public long LastServerTimeMs { get; private set; }
    public long LastBeatIndex { get; private set; }

    public float judgeWindowMs { get; set; } = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnBeatSync(BeatSyncInfo info)
    {
        Bpm = info.Bpm;
        BaseBeatDivision = info.BaseBeatDivision;
        ServerSongStartMs = info.SongStartServerTimeMs;
        LastBeatIndex = info.BeatIndex;

        // ✅ 여기서 offset 계산하지 않음!
        // 시간 보정은 Ping/Pong → TimeSync가 전담
        LastServerTimeMs = GetCurrentServerTimeMs();


    }

    public long GetCurrentServerTimeMs()
    {
        // ✅ RTT/오프셋 보정된 서버시간
        return TimeSync.ServerNowMs();
    }

    public long GetCurrentBeatIndex()
    {
        var serverNow = GetCurrentServerTimeMs();
        var elapsed = serverNow - ServerSongStartMs;
        if (elapsed < 0) return -1;

        double beatMs = GetBeatDurationMs();
        return (long)(elapsed / beatMs);
    }

    public double GetBeatDurationMs()
    {
        if (Bpm <= 0 || BaseBeatDivision <= 0)
            return 60000.0 / 120.0; // fallback 120bpm

        return 60000.0 / (Bpm * BaseBeatDivision);
    }

    public double GetCurrentBeatProgress01()
    {
        var serverNow = GetCurrentServerTimeMs();
        var elapsed = serverNow - ServerSongStartMs;
        if (elapsed < 0) return 0;

        double beatMs = GetBeatDurationMs();
        double beatIndex = elapsed / beatMs;
        double frac = beatIndex - Mathf.FloorToInt((float)beatIndex);
        return Mathf.Clamp01((float)frac);
    }
}

public struct BeatSyncInfo
{
    //public long ServerTimeMs;
    public long SongStartServerTimeMs;
    public double Bpm;
    public int BaseBeatDivision;
    public long BeatIndex;
}
