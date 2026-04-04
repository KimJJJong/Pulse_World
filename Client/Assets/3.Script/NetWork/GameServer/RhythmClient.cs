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

        Instance = this;
    }

    public void OnBeatSync(BeatSyncInfo info)
    {
        Bpm = info.Bpm;
        BaseBeatDivision = info.BaseBeatDivision;
        ServerSongStartMs = info.SongStartServerTimeMs;
        LastBeatIndex = info.BeatIndex;

        if (info.JudgeWindowMs > 0)
            judgeWindowMs = info.JudgeWindowMs;

        LastServerTimeMs = GetCurrentServerTimeMs();
    }


    public long GetJudgeTimeMs(long beatIndex)
    {
        // 서버와 동일하게: BeatIndex 기준 정확한 시간
        return GetBeatTimeMs(beatIndex);
    }
    public long GetBeatTimeMs(long beatIndex)
    {
        // beatIndex=0 이 SongStart 경계라고 가정
        if (beatIndex <= 0)
            return ServerSongStartMs;

        double beatMs = GetBeatDurationMs();
        // 반올림 정책: ms는 정수로 쓸 거라서 보통 Round가 안정적
        return ServerSongStartMs + (long)System.Math.Round(beatIndex * beatMs);
    }

    public long GetCurrentServerTimeMs()
    {
        //  RTT/오프셋 보정된 서버시간
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

    /// <summary>
    /// 서버 RhythmSystem.GetNearestBeatIndex와 동일: 가장 가까운 비트 반환 (반올림)
    /// </summary>
    public long GetNearestBeatIndex(long serverNowMs)
    {
        var elapsed = serverNowMs - ServerSongStartMs;
        if (elapsed < 0) return 0;

        double beatMs = GetBeatDurationMs();
        var idxFloat = elapsed / beatMs;
        return (long)System.Math.Round(idxFloat, System.MidpointRounding.AwayFromZero);
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
    public double GetTimeToNextBeatMs()
    {
        long beat = GetCurrentBeatIndex();
        if (beat < 0) return double.PositiveInfinity;

        long nextBeat = beat + 1;
        long nextBeatMs = GetBeatTimeMs(nextBeat);
        return nextBeatMs - GetCurrentServerTimeMs();
    }

    public double GetTimeFromPrevBeatMs()
    {
        long beat = GetCurrentBeatIndex();
        if (beat < 0) return double.PositiveInfinity;

        long currBeatMs = GetBeatTimeMs(beat);
        return GetCurrentServerTimeMs() - currBeatMs;
    }

    public long GetCurrentServerTick()
    {
        double diffMs = GetCurrentServerTimeMs() - ServerSongStartMs;
        if (diffMs < 0) return 0;
        
        double beatRatio = diffMs / GetBeatDurationMs();
        return (long)(beatRatio * 480);
    }}

public struct BeatSyncInfo
{
    public long SongStartServerTimeMs;
    public double Bpm;
    public int BaseBeatDivision;
    public long BeatIndex;

    public float JudgeWindowMs; // 추가
}

