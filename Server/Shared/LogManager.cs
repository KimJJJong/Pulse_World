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

		// [ping-fix] BoundedCapacity 를 두어서 로그 폭주 시에도 hot-path 가 블로킹되지 않게 한다.
		// 큐가 가득 차면 TryAdd 가 false 를 리턴하고 drop. 게임 서버에서 latency > 로그 신뢰성.
		private const int LogQueueCapacity = 8192;
		private readonly BlockingCollection<LogEntry> _logQueue =
			new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>(), LogQueueCapacity);

		private readonly string _logFilePath;
		private volatile bool _running = true;

		// [ping-fix] Console 미러링은 환경변수 RHYTHMRPG_LOG_CONSOLE=1 로만 켠다.
		// 원격 서버에서 stdout redirect 될 때 매 flush 마다 블로킹되면 수신 스레드까지 밀린다.
		private readonly bool _mirrorToConsole =
			string.Equals(Environment.GetEnvironmentVariable("RHYTHMRPG_LOG_CONSOLE"), "1", StringComparison.Ordinal);

		// [ping-fix] Flush 주기: 200ms 또는 64건마다. 매 엔트리마다 flush 하지 않는다.
		private const int FlushIntervalMs = 200;
		private const int FlushBatchCount = 64;

		// 드롭된 로그 카운터 (디버그용)
		private long _droppedCount;
		public long DroppedCount => System.Threading.Interlocked.Read(ref _droppedCount);

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
			// [ping-fix] Add → TryAdd. 큐가 가득 차면 드롭하고 계속 진행.
			if (!_logQueue.TryAdd(entry))
			{
				System.Threading.Interlocked.Increment(ref _droppedCount);
			}
		}

		public void LogInfo(string source, string message) => Log(LogLevel.Info, source, message);
		public void LogWarning(string source, string message) => Log(LogLevel.Warning, source, message);
		public void LogError(string source, string message) => Log(LogLevel.Error, source, message);
		public void LogDebug(string source, string message) => Log(LogLevel.Debug, source, message);

		private void ProcessLogQueue()
		{
			var jsonOptions = new JsonSerializerOptions
			{
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				WriteIndented = false
			};

			try
			{
				// [ping-fix] BufferedStream 으로 OS 쓰기 호출을 크게 줄인다.
				using (var fileStream = new FileStream(
					_logFilePath,
					FileMode.Append,
					FileAccess.Write,
					FileShare.Read,
					bufferSize: 64 * 1024,
					useAsync: false))
				using (var bufferedStream = new BufferedStream(fileStream, 64 * 1024))
				using (var streamWriter = new StreamWriter(bufferedStream, Encoding.UTF8))
				{
					int sinceLastFlushCount = 0;
					long lastFlushTicks = Environment.TickCount64;

					while (_running || _logQueue.Count > 0)
					{
						LogEntry entry;
						try
						{
							// [ping-fix] Take → TryTake(timeout).
							// 타임아웃으로 깨어나서 주기적으로 flush 할 수 있게 함.
							if (!_logQueue.TryTake(out entry, FlushIntervalMs))
							{
								// 타임아웃 → 현재까지의 버퍼를 flush
								if (sinceLastFlushCount > 0)
								{
									streamWriter.Flush();
									sinceLastFlushCount = 0;
									lastFlushTicks = Environment.TickCount64;
								}
								continue;
							}
						}
						catch (InvalidOperationException)
						{
							break;
						}

						string json = JsonSerializer.Serialize(entry, jsonOptions);
						streamWriter.WriteLine(json);
						sinceLastFlushCount++;

						// [ping-fix] Console 미러링은 옵션으로만. 기본 OFF.
						if (_mirrorToConsole)
						{
							Console.WriteLine(json);
						}

						// [ping-fix] 배치 크기 or 시간 경과 시에만 flush
						long nowTicks = Environment.TickCount64;
						if (sinceLastFlushCount >= FlushBatchCount ||
							(nowTicks - lastFlushTicks) >= FlushIntervalMs)
						{
							streamWriter.Flush();
							sinceLastFlushCount = 0;
							lastFlushTicks = nowTicks;
						}
					}

					// 종료 시 잔여분 flush
					streamWriter.Flush();
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
