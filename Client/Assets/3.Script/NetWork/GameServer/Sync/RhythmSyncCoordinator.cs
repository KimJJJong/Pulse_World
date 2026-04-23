using UnityEngine;

public static class RhythmSyncCoordinator
{
    const long WarnTooPastMs = -30_000;
    const long WarnTooFutureMs = 120_000;

    public static void ApplyHandshakeClock(long serverTimeMs)
    {
        if (serverTimeMs <= 0)
            return;

        TimeSync.SetOffsetFromServerNow(serverTimeMs);
    }

    public static bool TryApplyBeatSync(
        RhythmClient rhythm,
        long serverSendTimeMs,
        long songStartServerTimeMs,
        double bpm,
        int baseBeatDivision,
        long beatIndex,
        string sourceTag)
    {
        if (rhythm == null)
            return false;

        if (songStartServerTimeMs <= 0 || bpm <= 0 || baseBeatDivision <= 0)
        {
            Debug.LogWarning($"[RhythmSync] invalid payload source={sourceTag} start={songStartServerTimeMs} bpm={bpm} div={baseBeatDivision}");
            return false;
        }

        // BeatSync에도 서버 송신 시간이 있으므로, Pong이 오기 전이라도 한번 더 오프셋 보정한다.
        if (serverSendTimeMs > 0)
            TimeSync.SetOffsetFromServerNow(serverSendTimeMs);

        rhythm.OnBeatSync(new BeatSyncInfo
        {
            SongStartServerTimeMs = songStartServerTimeMs,
            Bpm = bpm,
            BaseBeatDivision = baseBeatDivision,
            BeatIndex = beatIndex
        });

        var untilStartMs = songStartServerTimeMs - rhythm.GetCurrentServerTimeMs();
        if (untilStartMs < WarnTooPastMs || untilStartMs > WarnTooFutureMs)
        {
            Debug.LogWarning($"[RhythmSync] unusual start window source={sourceTag} untilStartMs={untilStartMs} offset={TimeSync.OffsetMs:F1} rtt={TimeSync.EstimatedRttMs:F1}");
        }

        return true;
    }
}
