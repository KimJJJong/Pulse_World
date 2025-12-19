using System;
using System.Collections.Generic;

public sealed class ContentReport
{
    public string ContentName { get; }

    public int Success { get; private set; }
    public int Warnings { get; private set; }
    public int Errors { get; private set; }

    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages;

    public ContentReport(string contentName)
    {
        ContentName = contentName;
    }

    public void Ok(string message)
    {
        Success++;
        _messages.Add($"[OK] {message}");
    }

    public void Warn(string message)
    {
        Warnings++;
        _messages.Add($"[WARN] {message}");
    }

    public void Error(string message)
    {
        Errors++;
        _messages.Add($"[ERROR] {message}");
    }

    public override string ToString()
    {
        return $"{ContentName} Report: OK={Success}, WARN={Warnings}, ERROR={Errors}";
    }
}


public static class ContentReportPrinter
{
    public static void Print(ContentReport report)
    {
        Console.WriteLine("======================================");
        Console.WriteLine($"[Content] {report.ContentName}");
        Console.WriteLine($"  OK     : {report.Success}");
        Console.WriteLine($"  WARN   : {report.Warnings}");
        Console.WriteLine($"  ERROR  : {report.Errors}");
        Console.WriteLine("--------------------------------------");

        foreach (var msg in report.Messages)
        {
            if (msg.StartsWith("[ERROR]"))
                Console.ForegroundColor = ConsoleColor.Red;
            else if (msg.StartsWith("[WARN]"))
                Console.ForegroundColor = ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine(msg);
            Console.ResetColor();
        }

        Console.WriteLine("======================================");
    }
}