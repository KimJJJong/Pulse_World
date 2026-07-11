using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public sealed class P2PRelayMetrics
{
    private const int MaxElapsedSamples = 4096;
    private const int MaxQueueSamples = 4096;

    private readonly object _sampleLock = new();
    private readonly Queue<long> _forwardElapsedMs = new();
    private readonly Queue<int> _queuePending = new();
    private readonly long _startedAtMs;

    private long _relayRecvCount;
    private long _relayRecvBytes;
    private long _relaySendCount;
    private long _relaySendBytes;
    private long _relayForwardSuccess;
    private long _relayForwardFail;
    private long _relayDropCount;
    private long _relayRouteRejectCount;
    private long _relayAcceptedPayloads;
    private long _resultAccepted;
    private long _resultRejected;
    private long _lastActivityAtMs;

    public P2PRelayMetrics(string relayId, string mapId)
    {
        RelayId = relayId ?? "";
        MapId = mapId ?? "";
        _startedAtMs = NowMs();
        _lastActivityAtMs = _startedAtMs;
    }

    public string RelayId { get; }
    public string MapId { get; }

    public void RecordRelayRecv(int payloadBytes)
    {
        Interlocked.Increment(ref _relayRecvCount);
        Interlocked.Add(ref _relayRecvBytes, Math.Max(0, payloadBytes));
        MarkActivity();
    }

    public void RecordRelaySend(int targetCount, int packetBytes)
    {
        if (targetCount <= 0 || packetBytes <= 0)
            return;

        Interlocked.Add(ref _relaySendCount, targetCount);
        Interlocked.Add(ref _relaySendBytes, (long)targetCount * packetBytes);
        MarkActivity();
    }

    public void RecordForwardSuccess(long elapsedMs)
    {
        Interlocked.Increment(ref _relayForwardSuccess);
        RecordForwardElapsed(elapsedMs);
        MarkActivity();
    }

    public void RecordForwardFail(long elapsedMs)
    {
        Interlocked.Increment(ref _relayForwardFail);
        RecordForwardElapsed(elapsedMs);
        MarkActivity();
    }

    public void RecordDrop()
    {
        Interlocked.Increment(ref _relayDropCount);
        MarkActivity();
    }

    public void RecordRouteReject()
    {
        Interlocked.Increment(ref _relayRouteRejectCount);
        MarkActivity();
    }

    public void RecordAcceptedPayload()
    {
        Interlocked.Increment(ref _relayAcceptedPayloads);
        MarkActivity();
    }

    public void RecordResultAccepted()
    {
        Interlocked.Increment(ref _resultAccepted);
        MarkActivity();
    }

    public void RecordResultRejected()
    {
        Interlocked.Increment(ref _resultRejected);
        MarkActivity();
    }

    public void RecordQueuePending(int pending)
    {
        lock (_sampleLock)
        {
            if (_queuePending.Count >= MaxQueueSamples)
                _queuePending.Dequeue();

            _queuePending.Enqueue(Math.Max(0, pending));
        }
    }

    public P2PRelayMetricsSnapshot Snapshot()
    {
        long nowMs = NowMs();
        long durationMs = Math.Max(1, nowMs - _startedAtMs);
        double durationMinutes = durationMs / 60000.0;

        long[] elapsed;
        int[] queue;
        lock (_sampleLock)
        {
            elapsed = _forwardElapsedMs.ToArray();
            queue = _queuePending.ToArray();
        }

        long relaySendCount = Interlocked.Read(ref _relaySendCount);
        long relaySendBytes = Interlocked.Read(ref _relaySendBytes);
        long relayAcceptedPayloads = Interlocked.Read(ref _relayAcceptedPayloads);

        return new P2PRelayMetricsSnapshot
        {
            RelayId = RelayId,
            MapId = MapId,
            StartedAtMs = _startedAtMs,
            LastActivityAtMs = Interlocked.Read(ref _lastActivityAtMs),
            DurationSec = durationMs / 1000.0,
            RelayRecvCount = Interlocked.Read(ref _relayRecvCount),
            RelayRecvBytes = Interlocked.Read(ref _relayRecvBytes),
            RelaySendCount = relaySendCount,
            RelaySendBytes = relaySendBytes,
            RelaySendPerMinute = relaySendCount / durationMinutes,
            RelayBytesPerMinute = relaySendBytes / durationMinutes,
            RelaySendPerAcceptedPayload = relayAcceptedPayloads > 0
                ? (double)relaySendCount / relayAcceptedPayloads
                : 0,
            RelayBytesPerAcceptedPayload = relayAcceptedPayloads > 0
                ? (double)relaySendBytes / relayAcceptedPayloads
                : 0,
            RelayForwardSuccess = Interlocked.Read(ref _relayForwardSuccess),
            RelayForwardFail = Interlocked.Read(ref _relayForwardFail),
            RelayDropCount = Interlocked.Read(ref _relayDropCount),
            RelayRouteRejectCount = Interlocked.Read(ref _relayRouteRejectCount),
            RelayAcceptedPayloads = relayAcceptedPayloads,
            ResultAccepted = Interlocked.Read(ref _resultAccepted),
            ResultRejected = Interlocked.Read(ref _resultRejected),
            RelayForwardElapsedAvgMs = Average(elapsed),
            RelayForwardElapsedP95Ms = Percentile(elapsed, 0.95),
            RelayForwardElapsedMaxMs = elapsed.Length > 0 ? elapsed.Max() : 0,
            QueuePendingP95 = Percentile(queue, 0.95),
            QueuePendingMax = queue.Length > 0 ? queue.Max() : 0
        };
    }

    private void RecordForwardElapsed(long elapsedMs)
    {
        lock (_sampleLock)
        {
            if (_forwardElapsedMs.Count >= MaxElapsedSamples)
                _forwardElapsedMs.Dequeue();

            _forwardElapsedMs.Enqueue(Math.Max(0, elapsedMs));
        }
    }

    private void MarkActivity()
    {
        Interlocked.Exchange(ref _lastActivityAtMs, NowMs());
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static double Average(IReadOnlyCollection<long> values)
        => values.Count == 0 ? 0 : values.Average();

    private static double Percentile(long[] values, double percentile)
    {
        if (values.Length == 0)
            return 0;

        Array.Sort(values);
        int index = (int)Math.Ceiling(percentile * values.Length) - 1;
        index = Math.Clamp(index, 0, values.Length - 1);
        return values[index];
    }

    private static double Percentile(int[] values, double percentile)
    {
        if (values.Length == 0)
            return 0;

        Array.Sort(values);
        int index = (int)Math.Ceiling(percentile * values.Length) - 1;
        index = Math.Clamp(index, 0, values.Length - 1);
        return values[index];
    }
}

public sealed class P2PRelayMetricsSnapshot
{
    public string RelayId { get; set; } = "";
    public string MapId { get; set; } = "";
    public long StartedAtMs { get; set; }
    public long LastActivityAtMs { get; set; }
    public double DurationSec { get; set; }
    public long RelayRecvCount { get; set; }
    public long RelayRecvBytes { get; set; }
    public long RelaySendCount { get; set; }
    public long RelaySendBytes { get; set; }
    public double RelaySendPerMinute { get; set; }
    public double RelayBytesPerMinute { get; set; }
    public double RelaySendPerAcceptedPayload { get; set; }
    public double RelayBytesPerAcceptedPayload { get; set; }
    public long RelayForwardSuccess { get; set; }
    public long RelayForwardFail { get; set; }
    public long RelayDropCount { get; set; }
    public long RelayRouteRejectCount { get; set; }
    public long RelayAcceptedPayloads { get; set; }
    public long ResultAccepted { get; set; }
    public long ResultRejected { get; set; }
    public double RelayForwardElapsedAvgMs { get; set; }
    public double RelayForwardElapsedP95Ms { get; set; }
    public double RelayForwardElapsedMaxMs { get; set; }
    public double QueuePendingP95 { get; set; }
    public int QueuePendingMax { get; set; }
}
