using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

public sealed class DomainWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _tickMs;
    private readonly string _name;
    private readonly Func<IUpdatable[]> _snapshotGetter;

    public DomainWorker(string name, Func<IUpdatable[]> snapshotGetter, int tickMs)
    {
        _name = name;
        _snapshotGetter = snapshotGetter;
        _tickMs = tickMs;

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = name
        };
    }

    public void Start() => _thread.Start();

    private void Loop()
    {
        var sw = new Stopwatch();
        Console.WriteLine($"[{_name}] Started");

        while (!_cts.IsCancellationRequested)
        {
            sw.Restart();

            var list = _snapshotGetter(); // thread-safe snapshot
            foreach (var u in list)
            {
                try { u.Update(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_name}] Update exception: {ex}");
                }
            }

            // 고정밀 하이브리드 슬립 (Hybrid Precision Sleep)
            // 남은 시간을 계산하여, 2ms 이상 남았으면 OS Sleep으로 CPU를 양보하고
            // 마지막 1~2ms는 SpinWait로 대기하여 1ms 이하의 오차 정밀도를 확보합니다.
            long targetDuration = _tickMs * Stopwatch.Frequency / 1000;
            
            while (sw.ElapsedTicks < targetDuration)
            {
                long remainMs = (targetDuration - sw.ElapsedTicks) * 1000 / Stopwatch.Frequency;
                if (remainMs > 2)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }
            
            sw.Stop();
            long elapsed = sw.ElapsedMilliseconds;
            if (elapsed > _tickMs + 5) 
            {
                // 극심한 오버런인 경우에만 로깅
                // Console.WriteLine($"[{_name}] Tick Overrun: {elapsed}ms");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread.Join();
    }
}
