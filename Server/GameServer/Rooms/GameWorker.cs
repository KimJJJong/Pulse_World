//using System;
//using System.Threading;
//using System.Diagnostics;

//public sealed class GameWorker : IDisposable
//{
//    private readonly Thread _thread;
//    private readonly CancellationTokenSource _cts = new();
//    private readonly int _tickMs;

//    public GameWorker(int tickMs = 15)
//    {
//        _tickMs = tickMs;
//        _thread = new Thread(Loop)
//        {
//            IsBackground = true,
//            Name = "GameWorker"
//        };
//    }

//    public void Start() => _thread.Start();

//    private void Loop()
//    {
//        var sw = new Stopwatch();

//        Console.WriteLine("Worker Working");

//        while (!_cts.IsCancellationRequested)
//        {
//            sw.Restart();

//            // 1) 방 스냅샷 가져오기
//            var rooms = GameManager.GetUpdatablesSnapshot();

//            // 2) 각 방 Update 호출
//            foreach (var room in rooms)
//            {
//                try
//                {
//                    room.Update();
//                }
//                catch (Exception ex)
//                {
//                    // 방에서 예외 터져도 워커가 죽지 않도록 방어
//                    Console.WriteLine($"[GameWorker] Room.Update 예외: {ex}");
//                }
//            }

//            // 3) 남은 시간만큼 Sleep (고정틱)
//            sw.Stop();
//            int elapsed = (int)sw.ElapsedMilliseconds;
//            int delay = _tickMs - elapsed;
//            if (delay > 0)
//                Thread.Sleep(delay);
//            else
//            {
//                // 너무 오래 걸리면 다음 틱에 그냥 바로 진행
//                Console.WriteLine($"[GameWorker] Tick Overrun: {elapsed}ms");
//            }
//        }
//    }

//    public void Dispose()
//    {
//        _cts.Cancel();
//        _thread.Join();
//    }
//}
