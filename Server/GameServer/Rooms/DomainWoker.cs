using System;
using System.Threading;
using System.Diagnostics;

public sealed class DomainWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _tickMs;
    private readonly string _name;
    private readonly Func<IUpdatable[]> _snapshotGetter;

    public DomainWorker(string name, Func<IUpdatable[]> snapshotGetter, int tickMs)
    {
        _name           = name;
        _snapshotGetter = snapshotGetter;
        _tickMs         = tickMs;

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name         = name,
            // [RTT Fix] 게임 루프 스레드 우선순위 상향
            // - Town(33ms): AboveNormal → 패킷 큐 처리 지연 감소
            // - Game(15ms): AboveNormal → 리듬 판정 타이밍 정밀도 향상
            // Normal 이상으로 올리면 OS 스케줄러가 다른 작업보다 먼저 깨워줌
            Priority = ThreadPriority.AboveNormal,
        };
    }

    public void Start() => _thread.Start();

    private void Loop()
    {
        var sw = new Stopwatch();
        Console.WriteLine($"[{_name}] Started (tickMs={_tickMs})");

        // [RTT Fix] SpinWait 임계치 계산
        // Game(15ms): 마지막 1ms만 spin (SpinThresholdMs=1)
        // Town(33ms): 마지막 1ms만 spin
        // 이전 2ms spin -> Game 기준 매 틱 CPU 100% 구간이 2ms였음
        // -> 1ms로 줄여 패킷 처리 스레드에 CPU 양보 시간 확보
        const int SpinThresholdMs = 1;

        long targetTicks = _tickMs * Stopwatch.Frequency / 1000;

        while (!_cts.IsCancellationRequested)
        {
            sw.Restart();

            // Update 모든 방/월드
            var list = _snapshotGetter();
            foreach (var u in list)
            {
                try { u.Update(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_name}] Update exception: {ex}");
                }
            }

            // [RTT Fix] 하이브리드 슬립
            // remainMs > SpinThreshold → Thread.Sleep(1) : CPU 양보
            // remainMs <= SpinThreshold → SpinWait(5)    : 정밀 대기 (이전 10 → 5로 감소)
            while (sw.ElapsedTicks < targetTicks)
            {
                long remainMs = (targetTicks - sw.ElapsedTicks) * 1000 / Stopwatch.Frequency;
                if (remainMs > SpinThresholdMs)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(5); // 이전 10 → 5: CPU 낭비 절반으로 감소
            }

            // 오버런 경고 (디버그 시 주석 해제)
            // long elapsed = sw.ElapsedMilliseconds;
            // if (elapsed > _tickMs + 5)
            //     Console.WriteLine($"[{_name}] Overrun: {elapsed}ms (target={_tickMs}ms)");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread.Join();
    }
}
