/*namespace GameServer.InGame.Manager;

public sealed class BeatClock
{
    public int Bpm { get; private set; }
    public int TickRate { get; }
    public int TicksPerBeat { get; private set; }
    public int CurrentTick { get; private set; }
    public int CurrentBeatIndex { get; private set; }

    private readonly long _startTimeMs;

    public BeatClock(int bpm, int tickRate, long startTimeMs)
    {
        TickRate = tickRate;
        _startTimeMs = startTimeMs;
        SetBpm(bpm);
    }

    private void SetBpm(int bpm)
    {
        Bpm = bpm;
        TicksPerBeat = (TickRate * 60) / Bpm;
    }

    public void Tick(long nowMs)
    {
        CurrentTick = (int)((nowMs - _startTimeMs) * TickRate / 1000);
        CurrentBeatIndex = CurrentTick / TicksPerBeat;
    }

    public long GetBeatStartTimeMs(int beatIndex)
    {
        var beatDurationMs = 60000 / Bpm;
        return _startTimeMs + (long)beatIndex * beatDurationMs;
    }
}

*/