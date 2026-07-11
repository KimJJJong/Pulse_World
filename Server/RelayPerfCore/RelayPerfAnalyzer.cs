using System.Globalization;
using System.Text;

namespace RelayPerfCore;

public static class RelayPerfAnalyzer
{
    public const string CsvHeader =
        "captured_at,relay_id,map_id,duration_sec,relay_recv_count,relay_recv_bytes,relay_send_count,relay_send_bytes,relay_send_per_min,relay_bytes_per_min,relay_send_per_accepted_payload,relay_bytes_per_accepted_payload,relay_forward_success,relay_forward_fail,relay_drop_count,relay_route_reject_count,relay_accepted_payloads,result_accepted,result_rejected,relay_forward_avg_ms,relay_forward_p95_ms,relay_forward_max_ms,queue_pending_p95,queue_pending_max";

    public static IReadOnlyList<MetricRow> ReadLatestRowsFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Metric CSV was not found.", path);

        return ReadLatestRowsFromText(File.ReadAllText(path));
    }

    public static IReadOnlyList<MetricRow> ReadLatestRowsFromText(string csv)
    {
        var rows = ReadRowsFromText(csv);
        if (rows.Count == 0)
            throw new InvalidOperationException("No metric rows found.");

        return rows
            .GroupBy(row => string.IsNullOrWhiteSpace(row.RelayId) ? $"row-{row.Order}" : row.RelayId)
            .Select(group => group
                .OrderBy(row => row.CapturedAt ?? DateTimeOffset.MinValue)
                .ThenBy(row => row.DurationSec)
                .ThenBy(row => row.Order)
                .Last())
            .OrderBy(row => row.RelayId)
            .ToArray();
    }

    public static IReadOnlyList<MetricRow> ReadRowsFromText(string csv)
    {
        var lines = csv
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
            return Array.Empty<MetricRow>();

        var header = SplitCsv(lines[0]);
        var index = header
            .Select((name, i) => (name: name.Trim().ToLowerInvariant(), i))
            .GroupBy(x => x.name)
            .ToDictionary(x => x.Key, x => x.First().i, StringComparer.OrdinalIgnoreCase);

        var rows = new List<MetricRow>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var values = SplitCsv(lines[i]);
            rows.Add(new MetricRow
            {
                Order = i,
                CapturedAt = Date(index, values, "captured_at"),
                RelayId = Text(index, values, "relay_id"),
                MapId = Text(index, values, "map_id"),
                DurationSec = Double(index, values, "duration_sec"),
                RelayRecvCount = Long(index, values, "relay_recv_count"),
                RelayRecvBytes = Long(index, values, "relay_recv_bytes"),
                RelaySendCount = Long(index, values, "relay_send_count"),
                RelaySendBytes = Long(index, values, "relay_send_bytes"),
                RelaySendPerMinute = Double(index, values, "relay_send_per_min"),
                RelayBytesPerMinute = Double(index, values, "relay_bytes_per_min"),
                RelaySendPerAcceptedPayload = Double(index, values, "relay_send_per_accepted_payload"),
                RelayBytesPerAcceptedPayload = Double(index, values, "relay_bytes_per_accepted_payload"),
                RelayForwardSuccess = Long(index, values, "relay_forward_success"),
                RelayForwardFail = Long(index, values, "relay_forward_fail"),
                RelayDropCount = Long(index, values, "relay_drop_count"),
                RelayRouteRejectCount = Long(index, values, "relay_route_reject_count"),
                RelayAcceptedPayloads = Long(index, values, "relay_accepted_payloads"),
                ResultAccepted = Long(index, values, "result_accepted"),
                ResultRejected = Long(index, values, "result_rejected"),
                RelayForwardAvgMs = Double(index, values, "relay_forward_avg_ms"),
                RelayForwardP95Ms = Double(index, values, "relay_forward_p95_ms"),
                RelayForwardMaxMs = Double(index, values, "relay_forward_max_ms"),
                QueuePendingP95 = Double(index, values, "queue_pending_p95"),
                QueuePendingMax = Long(index, values, "queue_pending_max")
            });
        }

        return rows;
    }

    public static MetricSummary BuildSummary(string name, IReadOnlyList<MetricRow> rows)
    {
        long acceptedPayloads = rows.Sum(row => row.RelayAcceptedPayloads);
        long forwardAttempts = rows.Sum(row => row.RelayForwardSuccess + row.RelayForwardFail);

        return new MetricSummary
        {
            Name = name,
            RelayRooms = rows.Count,
            DurationSec = rows.Count == 0 ? 0 : rows.Max(row => row.DurationSec),
            RelayRecvCount = rows.Sum(row => row.RelayRecvCount),
            RelayRecvBytes = rows.Sum(row => row.RelayRecvBytes),
            RelaySendCount = rows.Sum(row => row.RelaySendCount),
            RelaySendBytes = rows.Sum(row => row.RelaySendBytes),
            RelaySendPerMinute = rows.Sum(row => row.RelaySendPerMinute),
            RelayBytesPerMinute = rows.Sum(row => row.RelayBytesPerMinute),
            RelayAcceptedPayloads = acceptedPayloads,
            RelaySendPerAcceptedPayload = acceptedPayloads > 0
                ? (double)rows.Sum(row => row.RelaySendCount) / acceptedPayloads
                : 0,
            RelayBytesPerAcceptedPayload = acceptedPayloads > 0
                ? (double)rows.Sum(row => row.RelaySendBytes) / acceptedPayloads
                : 0,
            RelayForwardSuccess = rows.Sum(row => row.RelayForwardSuccess),
            RelayForwardFail = rows.Sum(row => row.RelayForwardFail),
            RelayDropCount = rows.Sum(row => row.RelayDropCount),
            RelayRouteRejectCount = rows.Sum(row => row.RelayRouteRejectCount),
            ResultAccepted = rows.Sum(row => row.ResultAccepted),
            ResultRejected = rows.Sum(row => row.ResultRejected),
            RelayForwardAvgMs = forwardAttempts > 0
                ? rows.Sum(row => row.RelayForwardAvgMs * (row.RelayForwardSuccess + row.RelayForwardFail)) / forwardAttempts
                : 0,
            RelayForwardP95Ms = rows.Count == 0 ? 0 : rows.Max(row => row.RelayForwardP95Ms),
            RelayForwardMaxMs = rows.Count == 0 ? 0 : rows.Max(row => row.RelayForwardMaxMs),
            QueuePendingP95 = rows.Count == 0 ? 0 : rows.Max(row => row.QueuePendingP95),
            QueuePendingMax = rows.Count == 0 ? 0 : rows.Max(row => row.QueuePendingMax)
        };
    }

    public static RelayComparison BuildComparison(string leftName, IReadOnlyList<MetricRow> leftRows, string rightName, IReadOnlyList<MetricRow> rightRows)
        => BuildComparison(BuildSummary(leftName, leftRows), BuildSummary(rightName, rightRows));

    public static RelayComparison BuildComparison(MetricSummary left, MetricSummary right)
    {
        var ratios = new[]
        {
            BuildRatio("relay sends/min", left.RelaySendPerMinute, right.RelaySendPerMinute, FormatDouble),
            BuildRatio("relay bytes/min", left.RelayBytesPerMinute, right.RelayBytesPerMinute, FormatBytes),
            BuildRatio("sends per accepted payload", left.RelaySendPerAcceptedPayload, right.RelaySendPerAcceptedPayload, FormatDouble),
            BuildRatio("bytes per accepted payload", left.RelayBytesPerAcceptedPayload, right.RelayBytesPerAcceptedPayload, FormatBytes),
            BuildRatio("forward p95 ms", left.RelayForwardP95Ms, right.RelayForwardP95Ms, FormatMs),
            BuildRatio("queue p95", left.QueuePendingP95, right.QueuePendingP95, FormatDouble),
            BuildRatio("drops + rejects", left.RelayDropCount + left.RelayRouteRejectCount, right.RelayDropCount + right.RelayRouteRejectCount, FormatDouble)
        };

        return new RelayComparison
        {
            Left = left,
            Right = right,
            Ratios = ratios
        };
    }

    public static string RenderSummaryMarkdown(IEnumerable<MetricSummary> summaries)
    {
        var sb = new StringBuilder();
        AppendSummaryTable(sb, summaries.ToArray());
        return sb.ToString();
    }

    public static string RenderComparisonMarkdown(RelayComparison comparison)
    {
        var sb = new StringBuilder();
        AppendSummaryTable(sb, new[] { comparison.Left, comparison.Right });
        sb.AppendLine();
        AppendComparisonTable(sb, comparison);
        return sb.ToString();
    }

    public static string CreateTemplateCsv()
        => CsvHeader + Environment.NewLine;

    public static string FormatNumber(long value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string FormatDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string FormatSeconds(double value)
        => value.ToString("0.#", CultureInfo.InvariantCulture) + "s";

    public static string FormatMs(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + "ms";

    public static string FormatBytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;
        while (Math.Abs(value) >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.InvariantCulture) + units[unit];
    }

    public static string FormatRatio(double left, double right)
    {
        if (Math.Abs(left) < double.Epsilon)
            return Math.Abs(right) < double.Epsilon ? "1.000x" : "n/a";

        return (right / left).ToString("0.000", CultureInfo.InvariantCulture) + "x";
    }

    private static void AppendSummaryTable(StringBuilder sb, IReadOnlyList<MetricSummary> summaries)
    {
        sb.AppendLine("# Relay performance summary");
        sb.AppendLine();
        sb.AppendLine("| test | rooms | duration | relay sends | relay bytes | sends/min | bytes/min | sends/accepted | bytes/accepted | forward p95 | queue p95 | drops | rejects | result |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");

        foreach (var summary in summaries)
        {
            sb.AppendLine(
                $"| {EscapeMarkdown(summary.Name)} | {summary.RelayRooms} | {FormatSeconds(summary.DurationSec)} | {FormatNumber(summary.RelaySendCount)} | {FormatBytes(summary.RelaySendBytes)} | {FormatDouble(summary.RelaySendPerMinute)} | {FormatBytes(summary.RelayBytesPerMinute)} | {FormatDouble(summary.RelaySendPerAcceptedPayload)} | {FormatBytes(summary.RelayBytesPerAcceptedPayload)} | {FormatMs(summary.RelayForwardP95Ms)} | {FormatDouble(summary.QueuePendingP95)} | {FormatNumber(summary.RelayDropCount)} | {FormatNumber(summary.RelayRouteRejectCount)} | {summary.ResultAccepted}/{summary.ResultRejected} |");
        }
    }

    private static void AppendComparisonTable(StringBuilder sb, RelayComparison comparison)
    {
        sb.AppendLine("# Relay comparison");
        sb.AppendLine();
        sb.AppendLine("Ratio is right / left. Values below 1.0 mean the right case used less GameServer relay work for that metric.");
        sb.AppendLine();
        sb.AppendLine("| metric | " + EscapeMarkdown(comparison.Left.Name) + " | " + EscapeMarkdown(comparison.Right.Name) + " | ratio |");
        sb.AppendLine("|---|---:|---:|---:|");

        foreach (var row in comparison.Ratios)
            sb.AppendLine($"| {row.Metric} | {row.LeftDisplay} | {row.RightDisplay} | {row.RatioDisplay} |");
    }

    private static RatioRow BuildRatio(string metric, double left, double right, Func<double, string> formatter)
    {
        return new RatioRow
        {
            Metric = metric,
            LeftValue = left,
            RightValue = right,
            LeftDisplay = formatter(left),
            RightDisplay = formatter(right),
            RatioDisplay = FormatRatio(left, right),
            RatioValue = Math.Abs(left) < double.Epsilon ? null : right / left
        };
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    quoted = false;
                    continue;
                }

                current.Append(ch);
                continue;
            }

            if (ch == '"')
            {
                quoted = true;
                continue;
            }

            if (ch == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string Text(IReadOnlyDictionary<string, int> index, IReadOnlyList<string> values, string key)
        => index.TryGetValue(key, out int i) && i < values.Count ? values[i] : "";

    private static long Long(IReadOnlyDictionary<string, int> index, IReadOnlyList<string> values, string key)
        => long.TryParse(Text(index, values, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static double Double(IReadOnlyDictionary<string, int> index, IReadOnlyList<string> values, string key)
        => double.TryParse(Text(index, values, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static DateTimeOffset? Date(IReadOnlyDictionary<string, int> index, IReadOnlyList<string> values, string key)
        => DateTimeOffset.TryParse(Text(index, values, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;

    private static string EscapeMarkdown(string value)
        => (value ?? "").Replace("|", "\\|", StringComparison.Ordinal);
}
