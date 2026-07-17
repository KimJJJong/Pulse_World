using System.Threading.Tasks;
using System;
using Util;
using Shared;

public static class SessionSweeper
{
    static readonly TimeSpan IdleKick = TimeSpan.FromSeconds(15);

    // [ping-fix] 기존 100_000ms(100초) 주기는 idle kick 15초와 맞지 않아
    // 최대 115초까지 좀비 세션이 남을 수 있었음. 5초 주기로 조정.
    // 이 주기 자체는 핑에 영향 없지만 Sweeper 가 돌 때 전체 세션 스냅샷을 뜨므로
    // 너무 짧게 두면 안되고 5s 정도가 적절.
    static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    public static void Start()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                // [ping-fix] 매 주기마다 찍던 Console.WriteLine 제거.
                // 세션 0 이 아닐 때 현재 카운트를 LogManager 에 Debug 로 넘김.
                try
                {
                    int count = SessionManager.Instance.Count;
                    if (count != 0)
                    {
                        LogManager.Instance.LogDebug("SessionSweeper", $"Sweeping {count} client sessions");
                    }

                    var now = AppRef.ServerTimeMs();
                    foreach (var s in SessionManager.Instance.All()) // 세션 열람 API
                    {
                        var cs = s as ClientSession;
                        if (cs == null) continue;
                        var last = cs.LastPingAtMs;
                        if (last > 0 && (now - last) > IdleKick.TotalMilliseconds)
                        {
                            LogManager.Instance.LogWarning("SessionSweeper",
                                $"Idle kick: uid={cs.Uid} world={cs.CurrentWorldId} lastPing={last} now={now}");
                            cs.Close("PingPong Issue");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError("SessionSweeper", $"Sweep failed: {ex.Message}");
                }

                await Task.Delay(SweepInterval);
            }
        });
    }
}
