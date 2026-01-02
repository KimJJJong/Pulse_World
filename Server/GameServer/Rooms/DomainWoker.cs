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

            sw.Stop();
            int elapsed = (int)sw.ElapsedMilliseconds;
            int delay = _tickMs - elapsed;
            if (delay > 0) Thread.Sleep(delay);
            else Console.WriteLine($"[{_name}] Tick Overrun: {elapsed}ms");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread.Join();
    }
}
