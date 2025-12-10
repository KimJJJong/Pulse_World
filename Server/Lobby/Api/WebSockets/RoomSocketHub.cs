using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Lobby.Api.Config;
using Lobby.Domain.Rooms;
using Microsoft.Extensions.Options;
using Contracts.Packet;
using Lobby.Api.Middleware;
using Lobby.Logging;
using StackExchange.Redis;
using Lobby.Domain.Auth.Interface;
using Lobby.Infrastructure.Lifecycle;
using Lobby.Domain.Shared;
using Serilog;

namespace Lobby.Api.WebSockets;

public sealed class RoomSocketHub
{
    private readonly IRoomRepository _repo;
    private readonly IJwtService _jwt;
    private readonly ITicketIssuer _tickets;
    private readonly IRoomLifecycle _lifecycle;
    private readonly ConnectionRegistry _conns;
    private readonly AppOptions _opt;
    private readonly ConcurrentDictionary<string, TokenBucket> _readyBuckets = new();
    private readonly ILogger<RoomSocketHub> _logger;
    private readonly IGameServerResolver _gsResolver;
    private readonly IDatabase _redis; // mux.GetDatabase()

    public RoomSocketHub( IRoomRepository repo, IJwtService jwt, ITicketIssuer tickets,
                            ConnectionRegistry conns, IRoomLifecycle lifecycle,
                            IOptions<AppOptions> opt, ILogger<RoomSocketHub> logger,
                            IGameServerResolver gsResolver, IConnectionMultiplexer mux )
    {
        _repo = repo; _jwt = jwt; _tickets = tickets;
        _conns = conns; _lifecycle = lifecycle;
        _opt = opt.Value; _logger = logger;
        _gsResolver = gsResolver; _redis = mux.GetDatabase();
    }

    public async Task HandleAsync(string roomId, WebSocket ws, CancellationToken ct)
    {
        using var scopeRoom = _logger.BeginScope(new Dictionary<string, object?> { ["roomId"] = roomId });
        RoomHubLogs.WsAccept(_logger, roomId); 

        // 1) hello 수신 + 토큰/버전 검증
        var (ok, userId, clientVer) = await AuthenticateWithVersionAsync(ws, ct);
        if (!ok /*|| tokenRoomId != roomId*/)
        { RoomHubLogs.AuthFail(_logger, "not_authorized"); await CloseAsync(ws, "not_authorized", ct); return; }


        if (ClientVersionMiddleware.CompareSemVer(clientVer ?? "0.0.0", _opt.Versioning.MinClientVersion) < 0)
        { RoomHubLogs.VersionUnsupported(_logger, clientVer ?? "0.0.0", _opt.Versioning.MinClientVersion); await CloseAsync(ws, "client_version_unsupported", ct); return; }

        using var scopeUser = _logger.BeginScope(new Dictionary<string, object?> { ["userId"] = userId });

        // 2) 룸 조회/상태 확인
        var room = await _repo.GetAsync(roomId);
        if (room is null || room.Status is RoomStatus.Closed)
        { 
            RoomHubLogs.RoomNotFound(_logger, roomId);
            await CloseAsync(ws, "room_not_found", ct);
            return;
        }

        _lifecycle.CancelDeletion(room);

        // (재입장 케이스) 기존 유저에게 join 알림
        if (room.Members.TryGetValue(userId!, out var meExists))
        {
            await _conns.BroadcastAsync(roomId, new MemberJoinMsg { Member = meExists.ToContract() }, ct);
            RoomHubLogs.MemberJoinBroadcast(_logger, userId!); 
        }

        // 3) 연결 등록
        _conns.Add(roomId, userId!, ws);
        RoomHubLogs.WsJoin(_logger, userId!); 

        // 4) welcome 스냅샷
        var welcome = new WelcomeMsg
        {
            State = new WelcomeState
            {
                Room = room.ToContract(),
                Members = room.Members.Values.Select(m => m.ToContract()).ToList(),
                Countdown = (room.CountdownSeconds is null) ? null : new CountdownDto
                {
                    Seconds = room.CountdownSeconds.Value,
                    StartAtMs = room.CountdownStartAtMs!.Value
                }
            },
            ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await SendAsync(ws, welcome, ct);
        RoomHubLogs.WelcomeSent(_logger, welcome.State.Members.Count, welcome.State.Countdown != null); 

        // 5) 메시지 루프
        var buf = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var res = await ws.ReceiveAsync(buf, ct);
                if (res.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buf, 0, res.Count);
                var op = WireJson.Deserialize<OpOnly>(json)?.Op;

                switch (op)
                {
                    case "ready":
                        {
                            Console.WriteLine(json);
                            var msg = WireJson.Deserialize<ReadyMsg>(json)!;
                            string tmp = "Members : ";
                            foreach (var mem in room.Members.Values) tmp += mem.UserId + $"ReadyIs : {mem.Ready} ||";
                            Console.WriteLine($"Room:[ID : {room.Id}|| CurPlayer :{room.CurPlayers} ] Member :[{tmp}]");
                            await HandleReady(room, userId!, msg.Value, ct);
                            break;
                        }
                    case "leave":
                        {
                            await HandleLeave(room, userId!, ct);
                            await CloseAsync(ws, "bye", ct);
                            return;
                        }
                    default:
                        if (!string.IsNullOrEmpty(op)) RoomHubLogs.UnknownOp(_logger, op); 
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            RoomHubLogs.ReceiveLoopError(_logger, ex.Message, ex); 
        }
        finally
        {
            _conns.Remove(roomId, userId!, ws);
            RoomHubLogs.WsRemove(_logger); 
        }
    }

    // --- handlers --------------------------------------------------------------

    private async Task HandleReady(Room r, string userId, bool v, CancellationToken ct)
    {
        var bucket = _readyBuckets.GetOrAdd(userId, _ => new TokenBucket(_opt.RateLimit.ReadyTogglePerSec));
        if (!bucket.TryConsume())
        {
            RoomHubLogs.ReadyRateLimited(_logger, userId, _opt.RateLimit.ReadyTogglePerSec); 
            await _conns.BroadcastAsync(r.Id, new WarnMsg { Code = "rate_limited", Target = "ready", PerSec = _opt.RateLimit.ReadyTogglePerSec }, ct);
            return;
        }

        if (!r.Members.TryGetValue(userId, out var me)) return;
        if (me.Ready == v) return;

        me.Ready = v;
        await _repo.UpdateAsync(r);

        await _conns.BroadcastAsync(r.Id, new MemberUpdateMsg { Id = userId, Ready = v }, ct);
        RoomHubLogs.MemberUpdateBroadcast(_logger, userId, v); 

        if (r.Members.Count == 2 && r.Members.Values.All(m => m.Ready))
            await ArmCountdownAsync(r, ct);
        else
            await CancelCountdownAsync(r, ct);
    }

    private async Task HandleLeave(Room r, string userId, CancellationToken ct)
    {
        var ok = await _repo.LeaveAsync(r.Id, userId);
        await _repo.UpdateAsync(r);

        if (ok)
        {
            await _conns.BroadcastAsync(r.Id, new MemberLeaveMsg { Id = userId }, ct);
            RoomHubLogs.MemberLeave(_logger, userId); 
        }

        await _conns.BroadcastAsync(r.Id, new RoomUpdateMsg
        {
            Patch = new RoomPatch { Cur = r.CurPlayers, Status = r.Status.ToString(), UpdatedAtMs = r.UpdatedAtMs }
        }, ct);
        RoomHubLogs.RoomUpdate(_logger, r.CurPlayers, r.Status.ToString()); 

        if (r.CurPlayers == 0)
            _lifecycle.ScheduleDeletion(r, TimeSpan.FromSeconds(10));
    }

    private async Task ArmCountdownAsync(Room r, CancellationToken ct)
    {
        if (r.CountdownCts != null) return;

        r.CountdownCts = new CancellationTokenSource();
        r.CountdownSeconds = 1;
        r.CountdownStartAtMs = DateTimeOffset.UtcNow.AddSeconds(r.CountdownSeconds.Value).ToUnixTimeMilliseconds();

        await _conns.BroadcastAsync(r.Id, new CountdownStartMsg
        {
            Seconds = r.CountdownSeconds.Value,
            StartAtMs = r.CountdownStartAtMs.Value
        }, ct);
        RoomHubLogs.CountdownStart(_logger, r.CountdownSeconds.Value, r.CountdownStartAtMs.Value); 

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(r.CountdownSeconds!.Value), r.CountdownCts.Token);

                if (r.Members.Count == 2 && r.Members.Values.All(m => m.Ready))
                {
                    r.Status = RoomStatus.Starting;
                    await _repo.UpdateAsync(r);

                    // 1) Slot/Side 결정 (도메인에 Slot 있으면 그걸 쓰기)
                    var ordered = r.Members.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    var a = (uid: ordered[0], side: "A");
                    var b = (uid: ordered[1], side: "B");

                    // 2-1) Static GS선택
                    var (gsId, host, port, tickRate) = await _gsResolver.PickAsync(CancellationToken.None);
                    // 2-2)
                    int proto = _opt.Versioning.ProtoVer;


                    var tag = "{"+r.Id+"}";

                    // 3) 매치 메타 Redis 기록 (Lua 검증에서 사용)
                    await _redis.HashSetAsync($"match:{tag}", new HashEntry[] {
                        new("gsId", gsId), 
                        new("gsHost", host),
                        new("gsPort", port),
                        new("uidA", a.uid), 
                        new("uidB", b.uid),
                        new("protoVer", proto)
                });
                    await _redis.KeyExpireAsync($"match:{tag}", TimeSpan.FromMinutes(30));

                    // 4) 개인별 티켓
                    var ttl = TimeSpan.FromSeconds(_opt.Ticket.TtlSeconds);
                    var (toA, toB) = _tickets.IssueStartTickets(
                        matchId: r.Id, roomId: r.Id,
                        a, b,
                        gsHost: host, gsPort: port, 
                        tickRate: tickRate, ttl: ttl,
                        proto: proto );

                    // 5) 개별 전송 + 평탄화 host/port 채움
                    var msgA = new GameBeginMsg
                    {
                        Op = "game.begin",
                        GSAddress = new GameServerDto { Host = host, Port = port },
                        Ticket = toA.token,
                        ProtoVer = proto,
                    };
                    var msgB = new GameBeginMsg
                    {
                        Op = "game.begin",
                        GSAddress = new GameServerDto { Host = host, Port = port },
                        Ticket = toB.token,
                        ProtoVer = proto
                    };

                    await _conns.SendAsync(r.Id, a.uid, msgA, CancellationToken.None);
                    await _conns.SendAsync(r.Id, b.uid, msgB, CancellationToken.None);

                    RoomHubLogs.GameBegin(_logger, host, port, SafeTicket8(toA.token));
                }
            }
            catch (TaskCanceledException) { /* 취소됨 */ }
            catch (Exception ex)
            {
                RoomHubLogs.GameBeginError(_logger, "unexpected", ex);
            }
            finally
            {
                r.CountdownCts?.Dispose(); r.CountdownCts = null;
                r.CountdownSeconds = null; r.CountdownStartAtMs = null;
            }
        });
    }

    private async Task CancelCountdownAsync(Room r, CancellationToken ct)
    {
        if (r.CountdownCts is null) return;
        r.CountdownCts.Cancel();

        await _conns.BroadcastAsync(r.Id, new CountdownCancelMsg(), ct);
        RoomHubLogs.CountdownCancel(_logger); 

        r.CountdownSeconds = null;
        r.CountdownStartAtMs = null;
    }

    // --- util ------------------------------------------------------------------

    private static async Task SendAsync(WebSocket ws, object payload, CancellationToken ct)
    {
        var json = payload as string ?? WireJson.Serialize(payload);
        var seg = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(seg, WebSocketMessageType.Text, true, ct);
    }

    private static async Task CloseAsync(WebSocket ws, string reason, CancellationToken ct)
        => await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, ct);

    private async Task<(bool ok, string? userId, string? version)> AuthenticateWithVersionAsync(WebSocket ws, CancellationToken ct)
    {
        var buf = new byte[4096];
        var res = await ws.ReceiveAsync(buf, ct);
        var json = Encoding.UTF8.GetString(buf, 0, res.Count);

        var hello = WireJson.Deserialize<HelloMsg>(json);
        if (hello is null || hello.Op != "hello") 
            return (false, null, null);


        // AccessToken 검증
        var (ok, userId, claims, code) = _jwt.ValidateAccessToken(hello.Token);
        if (!ok || string.IsNullOrEmpty(userId))
            return (false, null, hello.Version);

        return (true, userId, hello.Version);
    }

    private static string SafeTicket8(string ticket)
        => string.IsNullOrEmpty(ticket) ? "" : ticket[..Math.Min(8, ticket.Length)];

    // 토큰버킷
    private sealed class TokenBucket
    {
        private readonly int _limit;
        private readonly int _perPeriod;
        private readonly TimeSpan _period = TimeSpan.FromSeconds(1);
        private int _tokens;
        private long _lastRefillTicks;

        public TokenBucket(int limitPerSec)
        {
            _limit = limitPerSec; _perPeriod = limitPerSec;
            _tokens = limitPerSec; _lastRefillTicks = DateTime.UtcNow.Ticks;
        }
        public bool TryConsume()
        {
            Refill();
            if (_tokens <= 0) return false;
            Interlocked.Decrement(ref _tokens);
            return true;
        }
        private void Refill()
        {
            var now = DateTime.UtcNow.Ticks;
            if (now - _lastRefillTicks >= _period.Ticks)
            {
                _tokens = Math.Min(_limit, _tokens + _perPeriod);
                _lastRefillTicks = now;
            }
        }
    }
}
