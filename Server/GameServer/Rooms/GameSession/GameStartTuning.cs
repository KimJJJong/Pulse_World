using Microsoft.Extensions.Configuration;
using Util;

public static class GameStartTuning
{
    const int DefaultReadyLeadMs = 1000;
    const int DefaultDisconnectGraceMs = 10_000;
    const int MinReadyLeadMs = 0;
    const int MaxReadyLeadMs = 30_000;
    const int MinDisconnectGraceMs = 1_000;
    const int MaxDisconnectGraceMs = 120_000;

    public static int ReadyLeadMs
        => Clamp(
            AppRef.Cfg?.GetValue<int?>("Server:GameStart:ReadyToBeginDelayMs") ?? DefaultReadyLeadMs,
            MinReadyLeadMs,
            MaxReadyLeadMs);

    public static int DisconnectGraceMs
        => Clamp(
            AppRef.Cfg?.GetValue<int?>("Server:GameStart:DisconnectGraceMs") ?? DefaultDisconnectGraceMs,
            MinDisconnectGraceMs,
            MaxDisconnectGraceMs);

    static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
