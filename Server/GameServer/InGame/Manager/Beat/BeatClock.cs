using System;

namespace GameServer.InGame.Manager.Beat;

public sealed class BeatClock : IBeatClock
{
    private readonly double _bpm;
    private readonly int _baseDiv; // ex) 4=쿼터 * 4 → 16분음표
    private readonly long _songStartMs;

    private double BaseBeatMs => 60000.0 / (_bpm * _baseDiv);

    public BeatClock(double bpm, int baseDiv, long songStartMs)
    {
        _bpm = bpm;
        _baseDiv = baseDiv;
        _songStartMs = songStartMs;
    }

    public long GetCurrentBeatIndex(long nowMs)
    {
        var elapsed = nowMs - _songStartMs;
        if (elapsed < 0) return -1;
        return (long)(elapsed / BaseBeatMs);
    }

    public long GetNearestBeatIndex(long nowMs)
    {
        var elapsed = nowMs - _songStartMs;
        if (elapsed < 0) return 0;

        var idxFloat = elapsed / BaseBeatMs;
        return (long)Math.Round(idxFloat, MidpointRounding.AwayFromZero);
    }

    public long GetBeatTimeMs(long beatIndex)
        => _songStartMs + (long)(beatIndex * BaseBeatMs);
}
