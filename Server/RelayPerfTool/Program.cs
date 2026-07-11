using System.Text;
using RelayPerfCore;

namespace RelayPerfTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            string command = args[0].ToLowerInvariant();
            var options = ParseOptions(args.Skip(1).ToArray());
            string output = command switch
            {
                "summarize" => RunSummarize(options),
                "compare" => RunCompare(options),
                "template" => RunTemplate(options),
                _ => throw new InvalidOperationException($"Unknown command: {args[0]}")
            };

            if (TryGet(options, "out", out var outputPath))
                File.WriteAllText(outputPath, output, Encoding.UTF8);

            Console.WriteLine(output);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("RelayPerfTool failed.");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string RunSummarize(IReadOnlyDictionary<string, string> options)
    {
        string input = Required(options, "input");
        string name = TryGet(options, "name", out var value)
            ? value
            : Path.GetFileNameWithoutExtension(input);

        var rows = RelayPerfAnalyzer.ReadLatestRowsFromFile(input);
        var summary = RelayPerfAnalyzer.BuildSummary(name, rows);
        return RelayPerfAnalyzer.RenderSummaryMarkdown(new[] { summary });
    }

    private static string RunCompare(IReadOnlyDictionary<string, string> options)
    {
        string leftPath = TryGet(options, "left", out var left)
            ? left
            : Required(options, "steam");
        string rightPath = TryGet(options, "right", out var right)
            ? right
            : Required(options, "server");

        string leftName = TryGet(options, "left-name", out var explicitLeftName)
            ? explicitLeftName
            : options.ContainsKey("steam") ? "SteamRelay" : Path.GetFileNameWithoutExtension(leftPath);
        string rightName = TryGet(options, "right-name", out var explicitRightName)
            ? explicitRightName
            : options.ContainsKey("server") ? "GameServerRelay" : Path.GetFileNameWithoutExtension(rightPath);

        var leftRows = RelayPerfAnalyzer.ReadLatestRowsFromFile(leftPath);
        var rightRows = RelayPerfAnalyzer.ReadLatestRowsFromFile(rightPath);
        var comparison = RelayPerfAnalyzer.BuildComparison(leftName, leftRows, rightName, rightRows);
        return RelayPerfAnalyzer.RenderComparisonMarkdown(comparison);
    }

    private static string RunTemplate(IReadOnlyDictionary<string, string> options)
    {
        string output = Required(options, "output");
        File.WriteAllText(output, RelayPerfAnalyzer.CreateTemplateCsv(), Encoding.UTF8);
        return $"Created CSV template: {output}";
    }

    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = arg[2..];
            string value = "true";
            if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[i + 1];
                i++;
            }

            options[key] = value;
        }

        return options;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> options, string key, out string value)
        => options.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);

    private static string Required(IReadOnlyDictionary<string, string> options, string key)
    {
        if (!TryGet(options, key, out var value))
            throw new InvalidOperationException($"Missing required option: --{key}");

        return value;
    }

    private static bool IsHelp(string value)
        => value is "-h" or "--help" or "help";

    private static void PrintHelp()
    {
        Console.WriteLine("""
        RelayPerfTool

        Commands:
          summarize --input <csv> [--name <label>] [--out <markdown>]
          compare --left <csv> --right <csv> [--left-name <label>] [--right-name <label>] [--out <markdown>]
          compare --steam <csv> --server <csv> [--out <markdown>]
          template --output <csv>

        Example:
          dotnet run --project Server/RelayPerfTool -- compare --steam steam.csv --server server.csv
        """);
    }
}
