namespace RelayPerfCore;

public sealed class MetricRow
{
    public int Order { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public string RelayId { get; init; } = "";
    public string MapId { get; init; } = "";
    public double DurationSec { get; init; }
    public long RelayRecvCount { get; init; }
    public long RelayRecvBytes { get; init; }
    public long RelaySendCount { get; init; }
    public long RelaySendBytes { get; init; }
    public double RelaySendPerMinute { get; init; }
    public double RelayBytesPerMinute { get; init; }
    public double RelaySendPerAcceptedPayload { get; init; }
    public double RelayBytesPerAcceptedPayload { get; init; }
    public long RelayForwardSuccess { get; init; }
    public long RelayForwardFail { get; init; }
    public long RelayDropCount { get; init; }
    public long RelayRouteRejectCount { get; init; }
    public long RelayAcceptedPayloads { get; init; }
    public long ResultAccepted { get; init; }
    public long ResultRejected { get; init; }
    public double RelayForwardAvgMs { get; init; }
    public double RelayForwardP95Ms { get; init; }
    public double RelayForwardMaxMs { get; init; }
    public double QueuePendingP95 { get; init; }
    public long QueuePendingMax { get; init; }
}

public sealed class MetricSummary
{
    public string Name { get; init; } = "";
    public int RelayRooms { get; init; }
    public double DurationSec { get; init; }
    public long RelayRecvCount { get; init; }
    public long RelayRecvBytes { get; init; }
    public long RelaySendCount { get; init; }
    public long RelaySendBytes { get; init; }
    public double RelaySendPerMinute { get; init; }
    public double RelayBytesPerMinute { get; init; }
    public long RelayAcceptedPayloads { get; init; }
    public double RelaySendPerAcceptedPayload { get; init; }
    public double RelayBytesPerAcceptedPayload { get; init; }
    public long RelayForwardSuccess { get; init; }
    public long RelayForwardFail { get; init; }
    public long RelayDropCount { get; init; }
    public long RelayRouteRejectCount { get; init; }
    public long ResultAccepted { get; init; }
    public long ResultRejected { get; init; }
    public double RelayForwardAvgMs { get; init; }
    public double RelayForwardP95Ms { get; init; }
    public double RelayForwardMaxMs { get; init; }
    public double QueuePendingP95 { get; init; }
    public long QueuePendingMax { get; init; }
}

public sealed class RatioRow
{
    public string Metric { get; init; } = "";
    public double LeftValue { get; init; }
    public double RightValue { get; init; }
    public double? RatioValue { get; init; }
    public string LeftDisplay { get; init; } = "";
    public string RightDisplay { get; init; } = "";
    public string RatioDisplay { get; init; } = "";
}

public sealed class RelayComparison
{
    public MetricSummary Left { get; init; } = new();
    public MetricSummary Right { get; init; } = new();
    public IReadOnlyList<RatioRow> Ratios { get; init; } = Array.Empty<RatioRow>();
}
