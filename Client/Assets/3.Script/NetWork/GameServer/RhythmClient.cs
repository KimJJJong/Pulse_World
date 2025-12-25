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

    // 클라-서버 시간 오프셋 추정 (serverTimeMs - localTimeMs)
    private double _serverTimeOffsetMs = 0;

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
        LastServerTimeMs = info.ServerTimeMs;
        LastBeatIndex = info.BeatIndex;

        long localNow = GetLocalTimeMs();
        _serverTimeOffsetMs = info.ServerTimeMs - localNow;

    }

    public long GetCurrentServerTimeMs()
    {
        long localNow = GetLocalTimeMs();
        return (long)(localNow + _serverTimeOffsetMs);
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
    /// <summary>
    /// 현재 Beat 안에서 0~1 진행률 (0: Beat 시작, 1: Beat 끝)
    /// </summary>
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


    private long GetLocalTimeMs()
    {
        // Unity에서 쓸 수 있는 간단한 시간 기준
        return (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}

public struct BeatSyncInfo
{
    public long ServerTimeMs;
    public long SongStartServerTimeMs;
    public double Bpm;
    public int BaseBeatDivision;
    public long BeatIndex;
}
