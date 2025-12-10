using System.Threading.Tasks;
using System;
using Util;
using Shared;

public static class SessionSweeper
{
    static readonly TimeSpan IdleKick = TimeSpan.FromSeconds(15);

    public static void Start()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                if( SessionManager.Instance.Count !=0 )
                Console.WriteLine($"[ Sweeping [ {SessionManager.Instance.Count} ] Client Sessions... ]");
                try
                {
                    var now = AppRef.ServerTimeMs();
                    foreach (var s in SessionManager.Instance.All()) // 세션 열람 API
                    {
                        var cs = s as ClientSession;
                        if (cs == null) continue;
                        var last = cs.LastPingAtMs;
                        if (last > 0 && (now - last) > IdleKick.TotalMilliseconds)
                        {
                            // 로그 남기고 정리
                            LogManager.Instance.LogWarning("[Ping/Pong Issue]",$"Idle kick: {cs.Uid}/{cs.MatchId}");
                            cs.Disconnect();
                        }
                    }
                }
                catch { /* swallow/log */ }

                await Task.Delay(10000);
            }
        });
    }
}
