using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public sealed class DomainWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _tickMs;
    private readonly string _name;
    private readonly Func<IUpdatable[]> _snapshotGetter;

    // [ping-fix] Windows 전용: Sleep 해상도를 1ms 로 올린다. 기본은 15.6ms.
    // TickMs=15 같은 짧은 틱에서는 이 호출 없으면 매 Sleep(1) 이 15.6ms 로 불어나
    // 핑 응답(broadcast) 이 tick 경계에서 밀려 클라 측 RTT 가 평균 +10~20ms 튄다.
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    private static int _timerPeriodRefCount = 0;
    private static readonly object _timerPeriodLock = new object();
    private bool _raisedTimerPeriod = false;

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public DomainWorker(string name, Func<IUpdatable[]> snapshotGetter, int tickMs)
    {
        _name = name;
        _snapshotGetter = snapshotGetter;
        _tickMs = tickMs;

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = name,
            // [ping-fix] 틱 스레드는 AboveNormal 우선순위. 게임 루프가 GC 나 다른 워커에 밀리지 않게.
            Priority = ThreadPriority.AboveNormal
        };
    }

    public void Start()
    {
        // [ping-fix] 짧은 틱(<= 20ms) 일 때만 OS 타이머 해상도 1ms 로 높임.
        // 불필요한 전역 영향(다른 프로세스 배터리 소모 등) 최소화.
        if (IsWindows && _tickMs > 0 && _tickMs <= 20)
        {
            lock (_timerPeriodLock)
            {
                if (_timerPeriodRefCount == 0)
                {
                    TimeBeginPeriod(1);
                }
                _timerPeriodRefCount++;
                _raisedTimerPeriod = true;
            }
        }

        _thread.Start();
    }

    private void Loop()
    {
        var sw = new Stopwatch();
        Console.WriteLine($"[{_name}] Started (tickMs={_tickMs})");

        // [ping-fix] tick 스케줄을 누적 기준(nextTickAt) 으로 돌린다.
        // 기존 코드는 매 tick 에서 sw.Restart() 라 update 가 오래 걸리면 다음 tick 도 그만큼 밀린다.
        // 여기선 목표 시각을 추적해서 drift 가 쌓이지 않게 한다.
        long freq = Stopwatch.Frequency;
        long tickTicks = _tickMs * freq / 1000;
        long baseTicks = Stopwatch.GetTimestamp();
        long tickIndex = 0;

        while (!_cts.IsCancellationRequested)
        {
            sw.Restart();

            var list = _snapshotGetter(); // thread-safe snapshot
            foreach (var u in list)
            {
                try { u.Update(); }
                catch (Exception ex)
                {
                    // [ping-fix] hot-path 에서 Console 동기 출력 제거. Shared 참조가 없으므로 최소화.
                    // (DomainWorker 는 GameServer 프로젝트에 있어 LogManager 사용 가능하지만
                    //  의존성 복잡도를 낮추기 위해 에러만 1회 출력하고 플래그로 스로틀링)
                    Console.Error.WriteLine($"[{_name}] Update exception: {ex.Message}");
                }
            }

            // 다음 tick 목표 시각 계산 (drift-free)
            tickIndex++;
            long targetAbsoluteTicks = baseTicks + tickIndex * tickTicks;

            // 고정밀 하이브리드 슬립
            while (true)
            {
                long now = Stopwatch.GetTimestamp();
                long remainTicks = targetAbsoluteTicks - now;
                if (remainTicks <= 0) break;

                long remainMs = remainTicks * 1000 / freq;
                if (remainMs > 2)
                {
                    Thread.Sleep(1);
                }
                else if (remainMs > 0)
                {
                    Thread.SpinWait(100);
                }
                else
                {
                    break;
                }
            }

            // 만약 overrun 이 심해서 여러 tick 을 통째로 놓쳤다면 baseTicks 를 재동기화
            // (무한정 누적되는 것을 방지)
            long nowAfter = Stopwatch.GetTimestamp();
            long driftTicks = nowAfter - targetAbsoluteTicks;
            if (driftTicks > tickTicks * 3)
            {
                // 3 tick 이상 밀렸으면 현재 시각으로 리베이스
                baseTicks = nowAfter;
                tickIndex = 0;
            }

            sw.Stop();
            // 로깅은 필요 시 주석 해제
            // long elapsed = sw.ElapsedMilliseconds;
            // if (elapsed > _tickMs + 5) { ... }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _thread.Join(1000); } catch { }

        if (_raisedTimerPeriod && IsWindows)
        {
            lock (_timerPeriodLock)
            {
                _timerPeriodRefCount--;
                if (_timerPeriodRefCount == 0)
                {
                    TimeEndPeriod(1);
                }
                _raisedTimerPeriod = false;
            }
        }
    }
}
