using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Workers;

public sealed class P2PRelayMetricsReporterHostedService : BackgroundService
{
    private const string CsvHeader =
        "captured_at,relay_id,map_id,duration_sec,relay_recv_count,relay_recv_bytes,relay_send_count,relay_send_bytes,relay_send_per_min,relay_bytes_per_min,relay_send_per_accepted_payload,relay_bytes_per_accepted_payload,relay_forward_success,relay_forward_fail,relay_drop_count,relay_route_reject_count,relay_accepted_payloads,result_accepted,result_rejected,relay_forward_avg_ms,relay_forward_p95_ms,relay_forward_max_ms,queue_pending_p95,queue_pending_max";

    private readonly ILogger<P2PRelayMetricsReporterHostedService> _log;
    private readonly object _writeLock = new();
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly string _outputDirectory;

    public P2PRelayMetricsReporterHostedService(
        ILogger<P2PRelayMetricsReporterHostedService> log,
        IConfiguration configuration)
    {
        _log = log;
        _enabled = configuration.GetValue("P2PRelayMetrics:Enabled", true);

        int intervalSeconds = configuration.GetValue("P2PRelayMetrics:IntervalSeconds", 300);
        if (intervalSeconds <= 0)
            intervalSeconds = 300;

        _interval = TimeSpan.FromSeconds(intervalSeconds);

        var configuredOutputDirectory = configuration["P2PRelayMetrics:OutputDirectory"];
        _outputDirectory = string.IsNullOrWhiteSpace(configuredOutputDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "metrics")
            : configuredOutputDirectory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _log.LogInformation("[P2PRelayMetrics] reporter disabled");
            return;
        }

        Directory.CreateDirectory(_outputDirectory);
        _log.LogInformation(
            "[P2PRelayMetrics] reporter started interval={IntervalSec}s output={Output}",
            _interval.TotalSeconds,
            _outputDirectory);

        try
        {
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    WriteSnapshot();
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[P2PRelayMetrics] snapshot write failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_enabled)
        {
            try
            {
                WriteSnapshot();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[P2PRelayMetrics] final snapshot write failed");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private void WriteSnapshot()
    {
        lock (_writeLock)
        {
            var snapshots = global::P2PRelayManager.GetMetricsSnapshot()
                .Concat(global::P2PRelayManager.DrainCompletedMetricsSnapshot())
                .ToArray();
            if (snapshots.Length == 0)
                return;

            string capturedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string csvPath = Path.Combine(_outputDirectory, "p2p-relay-metrics.csv");
            bool needsHeader = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;

            var csv = new StringBuilder();
            if (needsHeader)
                csv.AppendLine(CsvHeader);

            foreach (var snapshot in snapshots)
            {
                csv.AppendLine(ToCsvRow(capturedAt, snapshot));
                _log.LogInformation(
                    "[P2PRelayMetrics] relay={RelayId} duration={Duration:F1}s send={SendCount} bytes={SendBytes} sendPerMin={SendPerMin:F2} sendPerPayload={SendPerPayload:F2} forwardP95={ForwardP95:F2}ms queueP95={QueueP95:F0} drops={Drops} results={ResultAccepted}/{ResultRejected}",
                    snapshot.RelayId,
                    snapshot.DurationSec,
                    snapshot.RelaySendCount,
                    snapshot.RelaySendBytes,
                    snapshot.RelaySendPerMinute,
                    snapshot.RelaySendPerAcceptedPayload,
                    snapshot.RelayForwardElapsedP95Ms,
                    snapshot.QueuePendingP95,
                    snapshot.RelayDropCount,
                    snapshot.ResultAccepted,
                    snapshot.ResultRejected);
            }

            File.AppendAllText(csvPath, csv.ToString());
            File.WriteAllText(
                Path.Combine(_outputDirectory, "p2p-relay-metrics.latest.txt"),
                string.Join(Environment.NewLine + Environment.NewLine, snapshots.Select(ToSummary)));
        }
    }

    private static string ToCsvRow(string capturedAt, global::P2PRelayMetricsSnapshot s)
    {
        return string.Join(",", new[]
        {
            Escape(capturedAt),
            Escape(s.RelayId),
            Escape(s.MapId),
            Format(s.DurationSec),
            s.RelayRecvCount.ToString(CultureInfo.InvariantCulture),
            s.RelayRecvBytes.ToString(CultureInfo.InvariantCulture),
            s.RelaySendCount.ToString(CultureInfo.InvariantCulture),
            s.RelaySendBytes.ToString(CultureInfo.InvariantCulture),
            Format(s.RelaySendPerMinute),
            Format(s.RelayBytesPerMinute),
            Format(s.RelaySendPerAcceptedPayload),
            Format(s.RelayBytesPerAcceptedPayload),
            s.RelayForwardSuccess.ToString(CultureInfo.InvariantCulture),
            s.RelayForwardFail.ToString(CultureInfo.InvariantCulture),
            s.RelayDropCount.ToString(CultureInfo.InvariantCulture),
            s.RelayRouteRejectCount.ToString(CultureInfo.InvariantCulture),
            s.RelayAcceptedPayloads.ToString(CultureInfo.InvariantCulture),
            s.ResultAccepted.ToString(CultureInfo.InvariantCulture),
            s.ResultRejected.ToString(CultureInfo.InvariantCulture),
            Format(s.RelayForwardElapsedAvgMs),
            Format(s.RelayForwardElapsedP95Ms),
            Format(s.RelayForwardElapsedMaxMs),
            Format(s.QueuePendingP95),
            s.QueuePendingMax.ToString(CultureInfo.InvariantCulture)
        });
    }

    private static string ToSummary(global::P2PRelayMetricsSnapshot s)
    {
        return $"""
               [Relay Load]
               RelayId: {s.RelayId}
               MapId: {s.MapId}
               Duration: {s.DurationSec:F1}s
               RelayRecv: count {s.RelayRecvCount} / bytes {s.RelayRecvBytes}
               RelaySend: count {s.RelaySendCount} / bytes {s.RelaySendBytes} / perMin {s.RelaySendPerMinute:F2} / perAcceptedPayload {s.RelaySendPerAcceptedPayload:F2}
               ForwardTime: avg {s.RelayForwardElapsedAvgMs:F2} / p95 {s.RelayForwardElapsedP95Ms:F2} / max {s.RelayForwardElapsedMaxMs:F2} ms
               Queue: p95 {s.QueuePendingP95:F0} / max {s.QueuePendingMax} / drops {s.RelayDropCount} / rejects {s.RelayRouteRejectCount}
               Result: accepted {s.ResultAccepted} / rejected {s.ResultRejected}
               """;
    }

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        value ??= "";
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
