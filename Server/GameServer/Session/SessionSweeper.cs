using System.Threading.Tasks;
using System;
using Util;
using Shared;

public static class SessionSweeper
{
    // [RTT Fix] Idle 판정 기준: 마지막 Ping으로부터 15초 무응답 시 킥
    static readonly TimeSpan IdleKick = TimeSpan.FromSeconds(15);

    // [RTT Fix] 스윕 주기: 100000ms(100초!) -> 5000ms(5초)
    // 이전: Task.Delay(100000) -> 유령 세션이 최대 100초 동안 방치됨
    // 이후: Task.Delay(5000)   -> 5초마다 점검, 끊긴 세션 빠르게 정리
    const int SweepIntervalMs = 5000;

    public static void Start()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var count = SessionManager.Instance.Count;
                if (count != 0)
                    Console.WriteLine($"[ Sweeping [{count}] Client Sessions... ]");

                try
                {
                    var now = AppRef.ServerTimeMs();
                    foreach (var s in SessionManager.Instance.All())
                    {
                        var cs = s as ClientSession;
                        if (cs == null) continue;

                        var last = cs.LastPingAtMs;
                        if (last > 0 && (now - last) > IdleKick.TotalMilliseconds)
                        {
                            LogManager.Instance.LogWarning(
                                "[SessionSweeper]",
                                $"Idle kick: uid={cs.Uid} world={cs.CurrentWorldId} lastPing={now - last}ms ago");
                            cs.Close("PingPong Timeout");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SessionSweeper] Error: {ex.Message}");
                }

                await Task.Delay(SweepIntervalMs);
            }
        });
    }
}
