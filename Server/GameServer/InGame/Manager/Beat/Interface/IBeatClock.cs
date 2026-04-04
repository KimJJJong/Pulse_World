public interface IBeatClock
{
    long GetCurrentBeatIndex(long nowMs);    // 현재 Beat
    long GetCurrentTick(long nowMs);         // 경과 Tick (1 Beat = 480 Ticks 기준)
    long GetNearestBeatIndex(long nowMs);    // 가장 가까운 Beat (위/아래 중 더 가까운 쪽)
    long GetBeatTimeMs(long beatIndex);      // BeatIndex -> 서버 시간(ms)
    long GetJudgeTimeMs(long currentBeat, long nextBeat);     // Bpm, currentIndex 확인후 exBeat와 currentBeat

    long GetBeatDurationMs();


    bool TryComputeJudge(long nowMs, double actionWindowMs, out JudgeResult result);
}
