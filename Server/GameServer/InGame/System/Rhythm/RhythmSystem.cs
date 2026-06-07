using GameServer.InGame.Manager.Beat;
using System;

namespace GameServer.InGame.System.Rhythm;


public sealed class RhythmConfig
{
    public string SongKey { get; init; } = "DefaultSong";
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
    // Window 종료 시점 이벤트 추가
    public event Action<long> OnJudgeWindowEnd;

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

    public long GetCurrentTick(long nowMs)
    {
        var elapsed = nowMs - _songStartServerTimeMs;
        if (elapsed < 0) return 0;
        // 1 BaseBeatDivision 틱은 480 틱 (Tick)
        return (long)(elapsed * 480.0 / _baseBeatMs);
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
        // beatIndex에 해당하는 정확한 시간
        return _songStartServerTimeMs + (long)Math.Round(beatIndex * _baseBeatMs);
    }

    // 기존 유지 (중간 시간 구하기) - 쓰임처가 줄어들 수 있음
    public long GetJudgeTimeMs(long currentBeat, long nextBeat)
    {
        long currMs = GetBeatTimeMs(currentBeat);
        long nextMs = GetBeatTimeMs(nextBeat);
        return currMs + (nextMs - currMs) / 2;
    }

    // 수정: Round 권장
    public long GetBeatTimeMs(long beatIndex)
        => _songStartServerTimeMs + (long)Math.Round(beatIndex * _baseBeatMs);

    private long _lastWindowEndBeat = -1;

    public void Update()
    {
        var now = _time.NowMs;
        var beat = GetCurrentBeatIndex(now);

        // 1. OnBeat (Beat 시작점)
        if (beat > _lastProcessedBeat)
        {
            for (long b = _lastProcessedBeat + 1; b <= beat; b++)
            {
                _lastProcessedBeat = b;
                OnBeat?.Invoke(b);
            }
        }

        // 2. OnJudgeWindowEnd (Beat + Window)
        // 현재 시간 기준으로, "이미 지난 비트"들에 대해 윈도우가 닫혔는지 확인
        // 최적화를 위해 beat 범위 내에서 검사하거나, 그냥 간단히 loop
        // 여기서는 _lastWindowEndBeat를 두어 순차 처리
        
        // 검사할 후보: _lastWindowEndBeat + 1 부터 현재 beat까지?
        // 아니면 현재 시간 기준 역산?
        // 안전하게: _lastWindowEndBeat + 1 비트의 닫히는 시간 < now 이면 호출
        
        long targetBeat = _lastWindowEndBeat + 1;
        long targetBeatTime = GetBeatTimeMs(targetBeat);
        long windowEndTime = targetBeatTime + (long)_config.ActionWindowMs;

        while (now >= windowEndTime)
        {
            _lastWindowEndBeat = targetBeat;
            OnJudgeWindowEnd?.Invoke(targetBeat);

            targetBeat++;
            targetBeatTime = GetBeatTimeMs(targetBeat);
            windowEndTime = targetBeatTime + (long)_config.ActionWindowMs;
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

        // 1. 노래 시작 전
        if (nowMs < _songStartServerTimeMs)
        {
           // Console.WriteLine("[OnClientActionRequest] song not started yet");
            return false;
        }

        // 2. 가장 가까운 비트 찾기
        long nearestBeat = GetNearestBeatIndex(nowMs);
        long beatTime = GetBeatTimeMs(nearestBeat);

        long diff = nowMs - beatTime; // 양수면 늦게 침, 음수면 빨리 침
        long absDiff = Math.Abs(diff);

        bool accepted = absDiff <= actionWindowMs;

        // ExecuteBeat는 판정된 그 비트
        long executeBeat = nearestBeat;

        // 참고: currBeat는 시간상 "지나간" 비트 (floor)
        long currBeat = GetCurrentBeatIndex(nowMs);
        long nextBeat = currBeat + 1;

        result = new JudgeResult(
            currBeat: currBeat,
            nextBeat: nextBeat,
            centerMs: beatTime, // Center is now the Beat Time
            diffMs: (int)diff,
            accepted: accepted,
            executeBeat: executeBeat
        );

        return true;
    }

}
