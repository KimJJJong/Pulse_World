using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

namespace Shared
{
	public enum LogLevel
	{
		Info,
		Warning,
		Error,
		Debug
	}

	public class LogEntry
	{
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
		public LogLevel Level { get; set; }
		public string Source { get; set; } = "";
		public string Message { get; set; } = "";
	}

	public class LogManager
	{
		private static readonly Lazy<LogManager> _instance = new Lazy<LogManager>(() => new LogManager());
		public static LogManager Instance => _instance.Value;

		private readonly BlockingCollection<LogEntry> _logQueue = new BlockingCollection<LogEntry>();
		private readonly string _logFilePath;
		private volatile bool _running = true;

		private LogManager()
		{
			string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs_json");
			if (!Directory.Exists(logDirectory))
				Directory.CreateDirectory(logDirectory);

			_logFilePath = Path.Combine(logDirectory, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.json");

			Task.Factory.StartNew(ProcessLogQueue, TaskCreationOptions.LongRunning);
		}

		public void Log(LogLevel level, string source, string message)
		{
			var entry = new LogEntry
			{
				Timestamp = DateTime.UtcNow,
				Level = level,
				Source = source,
				Message = message
			};
			_logQueue.Add(entry);
		}

		public void LogInfo(string source, string message) => Log(LogLevel.Info, source, message);
		public void LogWarning(string source, string message) => Log(LogLevel.Warning, source, message);
		public void LogError(string source, string message) => Log(LogLevel.Error, source, message);
		public void LogDebug(string source, string message) => Log(LogLevel.Debug, source, message);

		private void ProcessLogQueue()
		{
			try
			{
				using (var streamWriter = new StreamWriter(_logFilePath, true, Encoding.UTF8))
				{
					while (_running || _logQueue.Count > 0)
					{
						LogEntry entry;
						try
						{
							entry = _logQueue.Take();
						}
						catch (InvalidOperationException)
						{
							break;
						}

						string json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
						{
							Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
							WriteIndented = false
						});
						streamWriter.WriteLine(json);
						streamWriter.Flush();

						Console.WriteLine(json); // Optional: 콘솔 출력
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"JSON 로그 처리 중 오류 발생: {ex.Message}");
			}
		}

		public void Shutdown()
		{
			LogInfo("System", "서버 종료중~~...");
			_running = false;
			_logQueue.CompleteAdding();
		}
	}
}