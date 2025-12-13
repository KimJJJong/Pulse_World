using GameServer.InGame.Manager.Beat;
using System;

namespace GameServer.InGame.System.Rhythm;


public sealed class RhythmConfig
{
    public double Bpm { get; init; }
    public int BaseBeatDivision { get; init; } // 예: 4 * 4 (16분음표)
    public double ActionWindowMs { get; init; } // Beat 판정 윈도우 (±)
    public int MaxBeatLookAhead { get; init; }  // 몇 Beat까지 미리 예약 허용
}
public sealed class RhythmSystem : IBeatClock
{
    private readonly IServerTime _time;
    private readonly RhythmConfig _config;
    private readonly long _songStartServerTimeMs;

    private readonly double _baseBeatMs;
    private long _lastProcessedBeat = -1;

    public event Action<long>? OnBeat; // 새 Beat마다 호출되는 이벤트

    public RhythmSystem(IServerTime time, RhythmConfig config, long songStartServerTimeMs)
    {
        _time = time;
        _config = config;
        _songStartServerTimeMs = songStartServerTimeMs;

        _baseBeatMs = 60000.0 / (config.Bpm * config.BaseBeatDivision);

    }

    // ===== IBeatClock 구현 =====

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

    public long GetBeatTimeMs(long beatIndex)
        => _songStartServerTimeMs + (long)(beatIndex * _baseBeatMs);

    // ===== 메인 업데이트 (서버 틱마다 호출) =====

    public void Update()
    {
        var now = _time.NowMs;
        var beat = GetCurrentBeatIndex(now);
        //Console.WriteLine($"BaseBeatMS : { _baseBeatMs } || Beat : {beat}");
        if (beat <= _lastProcessedBeat)
        {
            return;
        }
        // 중간 Beat가 여러 개 밀렸을 수 있으니, 하나씩 처리
        for (long b = _lastProcessedBeat + 1; b <= beat; b++)
        {
            _lastProcessedBeat = b;
            OnBeat?.Invoke(b);
        }
    }

    // 클라에 보내줄 Sync용 정보 (옵션)
    public SC_BeatSync CreateSyncPacket()
    {
        var now = _time.NowMs;
        var currBeat = GetCurrentBeatIndex(now);
        return new SC_BeatSync
        {
            ServerTimeMs = now,
            SongStartServerTimeMs = _songStartServerTimeMs,
            Bpm = _config.Bpm,
            BaseBeatDivision = _config.BaseBeatDivision,
            BeatIndex = currBeat,
        };
    }
}