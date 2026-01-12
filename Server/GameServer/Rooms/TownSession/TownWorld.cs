using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class TownWorld
{
    public string WorldId { get; }

    readonly object _lock = new();

    // (uid,epoch) -> PlayerRef (논리 플레이어)
    readonly Dictionary<(string uid, long epoch), PlayerRef> _players = new();

    // actorId -> PlayerRef (빠른 조회)
    readonly Dictionary<int, PlayerRef> _byActor = new();

    // slot -> PlayerRef (UI/자리)
    readonly Dictionary<int, PlayerRef> _bySlot = new();

    readonly SeatAllocator _seats;
    readonly ActorIdAllocator _actors = new();

    readonly int _maxPlayers;
    readonly int _graceMs;

    public TownWorld(string worldId, int maxPlayers = 64, int graceMs = 30000)
    {
        WorldId = worldId;
        _maxPlayers = maxPlayers;
        _graceMs = graceMs;
        _seats = new SeatAllocator(maxPlayers);
    }

    /// <summary>
    /// CS_MapEnter에서 호출: 플레이어를 월드에 입장시킴.
    /// 재연결이면 actorId 유지 + conn 교체.
    /// </summary>
    public EnterResult EnterOrReattach(ClientSession conn)
    {
        if (!conn.HasAuth)
            throw new InvalidOperationException("Unauthed connection cannot enter world.");

        lock (_lock)
        {
            var key = (conn.Uid, conn.Epoch);

            if (_players.TryGetValue(key, out var p))
            {
                // 재연결: actorId/slot 유지, conn만 교체
                p.Attach(conn);
                Index(p);
                return new EnterResult(isNew: false, p);
            }

            // 신규 입장
            if (_players.Count >= _maxPlayers)
                throw new InvalidOperationException("TownWorld full.");

            var slot = _seats.Alloc();
            var actorId = _actors.Next();

            var np = new PlayerRef(conn.Uid, conn.Epoch, actorId, slot);
            np.Attach(conn);

            _players[key] = np;
            Index(np);

            return new EnterResult(isNew: true, np);
        }
    }

    /// <summary>
    /// OnDisconnected/lease invalid 등에서 호출: 연결만 떼고 grace 타이머 시작.
    /// </summary>
    public void DetachIfMatch(string uid, long epoch, string connId)
    {
        PlayerRef? p = null;

        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out p))
                return;

            if (p.Conn == null || p.Conn.ConnId != connId)
                return; // 이미 다른 연결로 교체됨

            p.Detach();
            Unindex(p);

            // grace 시작 (재접속하면 Cancel됨)
            p.StartGrace(nowMs: NowMs(), graceMs: _graceMs, onExpired: () =>
            {
                // grace 만료 후 제거
                RemoveIfStillDetached(uid, epoch);
            });
        }
    }

    void RemoveIfStillDetached(string uid, long epoch)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
                return;

            // 재연결되어 conn가 붙었으면 제거하지 않음
            if (p.Conn != null)
                return;

            _players.Remove((uid, epoch));
            _byActor.Remove(p.ActorId);
            _bySlot.Remove(p.Slot);

            _seats.Free(p.Slot);

            // TODO: 엔티티/리소스 정리 (플레이어 엔티티 despawn broadcast 등)
        }
    }

    void Index(PlayerRef p)
    {
        _byActor[p.ActorId] = p;
        _bySlot[p.Slot] = p;
    }

    void Unindex(PlayerRef p)
    {
        _byActor.Remove(p.ActorId);
        _bySlot.Remove(p.Slot);
    }

    public IReadOnlyList<PlayerSnapshot> BuildPlayersSnapshot()
    {
        lock (_lock)
        {
            return _players.Values
                .Select(p => new PlayerSnapshot(p.Uid, p.Epoch, p.ActorId, p.Slot, isOnline: p.Conn != null))
                .OrderBy(x => x.Slot)
                .ToList();
        }
    }

    public void Broadcast(ArraySegment<byte> sendBuf)
    {
        ClientSession[] conns;
        lock (_lock)
        {
            conns = _players.Values
                .Select(p => p.Conn)
                .Where(c => c != null && c.IsConnected)
                .Cast<ClientSession>()
                .ToArray();
        }

        foreach (var c in conns)
            c.Send(sendBuf);
    }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ---------- Nested types ----------

    public readonly struct EnterResult
    {
        public readonly bool IsNew;
        public readonly PlayerRef Player;

        public EnterResult(bool isNew, PlayerRef player)
        {
            IsNew = isNew;
            Player = player;
        }
    }

    public sealed class PlayerRef
    {
        public string Uid { get; }
        public long Epoch { get; }
        public int ActorId { get; }
        public int Slot { get; }

        public ClientSession? Conn { get; private set; }

        // grace
        long _graceUntilMs;
        int _graceToken; // 재연결 시 이전 grace 무효화

        public PlayerRef(string uid, long epoch, int actorId, int slot)
        {
            Uid = uid;
            Epoch = epoch;
            ActorId = actorId;
            Slot = slot;
        }

        public void Attach(ClientSession conn)
        {
            Conn = conn;
            conn.CurrentWorldId = "Town_01"; // ★ 여기서 월드ID를 박아도 되고, 상위에서 넣어도 됨
            _graceUntilMs = 0;
            _graceToken++;
        }

        public void Detach()
        {
            if (Conn != null)
                Conn.CurrentWorldId = "";
            Conn = null;
        }

        public void StartGrace(long nowMs, int graceMs, Action onExpired)
        {
            var token = _graceToken;
            _graceUntilMs = nowMs + graceMs;

            _ = Task.Run(async () =>
            {
                var delay = graceMs;
                await Task.Delay(delay);

                // 재연결로 token이 바뀌었으면 무효
                if (_graceToken != token)
                    return;

                onExpired();
            });
        }
    }

    public readonly struct PlayerSnapshot
    {
        public readonly string Uid;
        public readonly long Epoch;
        public readonly int ActorId;
        public readonly int Slot;
        public readonly bool IsOnline;

        public PlayerSnapshot(string uid, long epoch, int actorId, int slot, bool isOnline)
        {
            Uid = uid;
            Epoch = epoch;
            ActorId = actorId;
            Slot = slot;
            IsOnline = isOnline;
        }
    }

    sealed class SeatAllocator
    {
        readonly Queue<int> _free = new();

        public SeatAllocator(int max)
        {
            for (int i = 0; i < max; i++) _free.Enqueue(i);
        }

        public int Alloc()
        {
            if (_free.Count == 0) throw new Exception("no seat");
            return _free.Dequeue();
        }

        public void Free(int slot) => _free.Enqueue(slot);
    }

    sealed class ActorIdAllocator
    {
        int _next = 1000;
        public int Next() => _next++;
    }
}
