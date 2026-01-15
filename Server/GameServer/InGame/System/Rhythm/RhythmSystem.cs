using GameServer.InGame.Manager.Beat;
using System;

namespace GameServer.InGame.System.Rhythm;


public sealed class RhythmConfig
{
    public double Bpm { get; init; }
    public int BaseBeatDivision { get; init; } // 예: 4 * 4 (16분음표)
    public double ActionWindowMs { get; init; } // Beat 판정 윈도우 (+-)
    public int MaxBeatLookAhead { get; init; }  // 몇 Beat까지 미리 예약 허용

    //public int LeadBeats { get; set; }
}
public sealed class RhythmSystem : IBeatClock
{
    private readonly IServerTime _time;
    private readonly RhythmConfig _config;
    private readonly long _songStartServerTimeMs;

    private readonly double _baseBeatMs;
    private long _lastProcessedBeat = -1;

    public event Action<long> OnBeat;

    public RhythmSystem(IServerTime time, RhythmConfig config, long songStartServerTimeMs)
    {
        _time = time;
        _config = config;
        _songStartServerTimeMs = songStartServerTimeMs;

        _baseBeatMs = 60000.0 / (config.Bpm * config.BaseBeatDivision);
    }

    // 추가: 외부에서 디버그/동기화에 쓰기 좋게 공개
    public long GetBeatDurationMs() => (long)_baseBeatMs;
    public long SongStartServerTimeMs => _songStartServerTimeMs;
    public RhythmConfig Config => _config;

    public long GetCurrentBeatIndex(long nowMs)
    {
        var elapsed = nowMs - _songStartServerTimeMs;
        if (elapsed < 0) return -1;
        return (long)(elapsed / _baseBeatMs);
    }

    public long GetNearestBeatIndex(long nowMs)
    {
        var elapsed = nowMs - _songStartServerTimeMs;
        if (elapsed < 0) return 0;

        var idxFloat = elapsed / _baseBeatMs;
        return (long)Math.Round(idxFloat, MidpointRounding.AwayFromZero);
    }

    // 추가: 클라와 같은 시그니처
    public long GetJudgeTimeMs(long beatIndex)
    {
        if (beatIndex <= 0) return GetBeatTimeMs(0);
        long prev = GetBeatTimeMs(beatIndex - 1);
        long cur = GetBeatTimeMs(beatIndex);
        return prev + (cur - prev) / 2;
    }

    // 기존 유지
    public long GetJudgeTimeMs(long currentBeat, long nextBeat)
    {
        long currMs = GetBeatTimeMs(currentBeat);
        long nextMs = GetBeatTimeMs(nextBeat);
        return currMs + (nextMs - currMs) / 2;
    }

    // 수정: Round 권장
    public long GetBeatTimeMs(long beatIndex)
        => _songStartServerTimeMs + (long)Math.Round(beatIndex * _baseBeatMs);

    public void Update()
    {
        var now = _time.NowMs;
        var beat = GetCurrentBeatIndex(now);
        if (beat <= _lastProcessedBeat) return;

        for (long b = _lastProcessedBeat + 1; b <= beat; b++)
        {
            _lastProcessedBeat = b;
            OnBeat?.Invoke(b);
        }
    }

    // 디버그 바 유틸
    public static string FormatJudgeBar(
        long currBeat, long nextBeat,
        long nowMs, long judgeCenterMs,
        int windowMs,
        int halfSpanMs,
        int width = 36,
        char marker = '^')
    {
        halfSpanMs = Math.Max(halfSpanMs, windowMs + 1);

        long startMs = judgeCenterMs - halfSpanMs;
        long endMs = judgeCenterMs + halfSpanMs;

        double t = (nowMs - startMs) / (double)(endMs - startMs);
        int pos = (int)Math.Round(t * (width - 1));
        pos = Math.Clamp(pos, 0, width - 1);

        int center = width / 2;

        int winHalf = (int)Math.Round(windowMs / (double)halfSpanMs * (width / 2.0));
        winHalf = Math.Clamp(winHalf, 0, center);

        int winL = center - winHalf;
        int winR = center + winHalf;

        var chars = new char[width];
        for (int i = 0; i < width; i++)
            chars[i] = (i >= winL && i <= winR) ? '=' : '-';

        chars[center] = '|';
        chars[pos] = marker;

        long diff = nowMs - judgeCenterMs;
        return $"curBeat[{new string(chars)}]nextBeat diff={diff}ms win=±{windowMs}ms";
    }





    public bool TryComputeJudge(long nowMs, double actionWindowMs, out JudgeResult result)
    {
        result = default;

        var currBeat = GetCurrentBeatIndex(nowMs);
        if (currBeat < 0)
        {
            Console.WriteLine("[OnClientActionRequest] song not started yet");
            return false;
        }

        var nextBeat = currBeat + 1;
        var centerMs = GetJudgeTimeMs(currBeat, nextBeat);

        var diff = (int)(nowMs - centerMs);
        var abs = Math.Abs(diff);

        bool accepted = abs <= actionWindowMs;
        long executeBeat = nextBeat; // 네 정책: 항상 nextBeat로 묶기

        result = new JudgeResult(
            currBeat: currBeat,
            nextBeat: nextBeat,
            centerMs: centerMs,
            diffMs: diff,
            accepted: accepted,
            executeBeat: executeBeat
        );

        return true;
    }

}
