using Microsoft.Extensions.Configuration;
using Server.withWebServer.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Util
{
    public static class AppRef
    {
        public static JwtService Jwt = null!;
        public static IConfiguration Cfg = null!;
        public static CancellationTokenSource Cts = new();
        static readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        public static int ProtoVer => Cfg.GetValue<int>("Versioning:ProtoVer", 1);
        public static int TickRate => Cfg.GetValue<int>("GameServer:TickRate", 30);
        public static string GSId => Cfg.GetValue<string>("GameServer:Id");

        public static long ServerTimeMs() => _sw.ElapsedMilliseconds;
    }
}

