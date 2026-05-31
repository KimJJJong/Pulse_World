using GameServer.Content.Map;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public abstract class RoomBase : IGameBroadcaster, IUpdatable
{
    protected readonly object _lock = new();

    // actorId -> session
    protected readonly Dictionary<int, ClientSession?> _byActor = new();

    // (uid, epoch) -> player
    protected readonly Dictionary<(string uid, long epoch), RoomPlayer> _players = new();

    protected ClientSession[] _broadcastSnapshot = Array.Empty<ClientSession>();
    protected bool _broadcastDirty = true;

    protected readonly Queue<int> _freeSeats = new();
    
    // Abstract Session Access for children
    protected abstract SessionBase? GetSession();
    protected abstract bool IsRoomRunning();
    protected virtual string RoomLogKind => GetType().Name;
    protected virtual string RoomLogId => "";

    protected readonly int _maxPlayers;
    protected int _nextActorId;

    protected RoomBase(int maxPlayers, int startActorId)
    {
        _maxPlayers = Math.Max(1, maxPlayers);
        _nextActorId = startActorId;
        for (int i = 0; i < _maxPlayers; i++)
            _freeSeats.Enqueue(i);
    }

    public virtual bool BindOrReattach(ClientSession s, out int actorId)
    {
        actorId = -1;
        if (s == null)
        {
            LogRoomBindFail(null, "null_session");
            return false;
        }

        if (!s.HasAuth || string.IsNullOrEmpty(s.Uid))
        {
            LogRoomBindFail(s, "not_authenticated");
            return false;
        }

        bool isNew = false;
        int seat = -1;
        int playerCount = -1;
        RoomPlayer? p = null;

        lock (_lock)
        {
            if (CheckRoomEnded())
            {
                LogRoomBindFail(s, "room_ended", _players.Count);
                return false;
            }

            var key = (s.Uid, s.Epoch);

            if (_players.TryGetValue(key, out var existing))
            {
                // Reattach
                p = existing;
                actorId = p.ActorId;
                seat = p.SeatIndex;

                p.Attach(s);
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = seat;
                UpdateSessionWorldId(s);

                _broadcastDirty = true;
                isNew = false;
                playerCount = _players.Count;
            }
            else
            {
                // New Join
                if (_players.Count >= _maxPlayers)
                {
                    LogRoomBindFail(s, "room_full", _players.Count);
                    return false;
                }
                if (_freeSeats.Count == 0 && _players.Count < _maxPlayers)
                {
                    // Should not happen if logic is correct, but safety
                    // [ping-fix] Console.WriteLine → LogManager
                    LogManager.Instance.LogError("RoomBase",
                        $"FreeSeats empty but room not full. players={_players.Count} max={_maxPlayers}");
                    LogRoomBindFail(s, "seat_pool_corrupt", _players.Count);
                    return false;
                }
                if (_freeSeats.Count == 0)
                {
                    LogRoomBindFail(s, "no_free_seat", _players.Count);
                    return false;
                }

                actorId = _nextActorId++;
                seat = _freeSeats.Dequeue();

                p = new RoomPlayer(s.Uid, s.Epoch, actorId, seat);
                p.Attach(s);

                _players[key] = p;
                _byActor[actorId] = s;

                s.ActorId = actorId;
                s.SeatIndex = seat;
                UpdateSessionWorldId(s);

                _broadcastDirty = true;
                isNew = true;
                playerCount = _players.Count;
            }
        }

        LogRoomBind(s, actorId, seat, isNew, playerCount);
        OnPlayerBound(p, isNew);
        return true;
    }

    public virtual void DetachIfMatch(string uid, long epoch, string connId)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
            {
                LogRoomDetachSkip(uid, epoch, connId, "player_not_found", -1, -1, "-");
                return;
            }

            var cur = p.Conn;
            if (cur == null || cur.ConnId != connId)
            {
                LogRoomDetachSkip(uid, epoch, connId, cur == null ? "already_detached" : "conn_mismatch", p.ActorId, p.SeatIndex, cur?.ConnId ?? "-");
                return;
            }

            _byActor[p.ActorId] = null;
            p.Detach();

            _broadcastDirty = true;
            LogRoomDetach(uid, epoch, connId, p.ActorId, p.SeatIndex, _players.Count);
        }
    }

    public virtual void RemovePlayer(string uid, long epoch)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
            {
                LogRoomRemoveSkip(uid, epoch, "player_not_found");
                return;
            }

            var actorId = p.ActorId;
            var seatIndex = p.SeatIndex;
            var connId = p.Conn?.ConnId ?? "-";
            _players.Remove((uid, epoch));
            _byActor.Remove(actorId);

            _freeSeats.Enqueue(seatIndex);

            _broadcastDirty = true;

            // [ping-fix] Console.WriteLine → LogManager
            LogManager.Instance.LogInfo("RoomBase",
                $"RemovePlayer uid={uid} epoch={epoch} remaining={_players.Count}");
            LogRoomRemove(uid, epoch, connId, actorId, seatIndex, _players.Count);

            // Call Session OnPlayerLeft via Queue
            Enqueue(() => GetSession()?.OnPlayerLeft(actorId));
        }

        MaybeEndIfEmpty();
    }

    protected abstract void UpdateSessionWorldId(ClientSession s);
    protected abstract bool CheckRoomEnded();
    protected abstract void MaybeEndIfEmpty();

    protected virtual void OnPlayerBound(RoomPlayer p, bool isNew)
    {
        // Default behavior: Enqueue init packet if room is running
        Enqueue(() =>
        {
            var session = GetSession();
            if (!IsRoomRunning() || session == null) return;

            if (isNew)
            {
                 // New player logic (handled by child usually, or we can abstract)
                 OnNewPlayerJoinedQueue(p, session);
            }
            else
            {
                 // Reattach logic
                 session.EnsurePlayerSpawned(p.ActorId);
            }

            if (p.Conn != null)
                session.SendInitPacketToPlayer(p.Conn);
        });
    }

    protected virtual void OnNewPlayerJoinedQueue(RoomPlayer p, SessionBase session)
    {
        // Default: just spawn him?
        // TownRoom creates Entity here. GameRoom creates Entity at start.
        // So this might need to be overridden.
    }
    
    // ===================================
    // Broadcaster
    // ===================================
    public void Broadcast(IPacket pkt) => Broadcast(pkt.Write());

    public void Broadcast(ArraySegment<byte> payload)
    {
        var targets = GetBroadcastSnapshot();
        foreach (var t in targets)
            t.Send(payload);
    }

    public void BroadcastExcept(IPacket pkt, ClientSession? except)
        => BroadcastExcept(pkt.Write(), except);

    public void BroadcastExcept(ArraySegment<byte> payload, ClientSession? except)
    {
        var targets = GetBroadcastSnapshot();
        foreach (var t in targets)
        {
            if (except != null && ReferenceEquals(t, except))
                continue;

            t.Send(payload);
        }
    }

    protected ClientSession[] GetBroadcastSnapshot()
    {
        lock (_lock)
        {
            if (!_broadcastDirty)
                return _broadcastSnapshot;

            var list = new List<ClientSession>(_byActor.Count);
            foreach (var s in _byActor.Values)
            {
                if (s != null && s.IsConnected)
                    list.Add(s);
            }

            _broadcastSnapshot = list.ToArray();
            _broadcastDirty = false;
            return _broadcastSnapshot;
        }
    }

    // ===================================
    // Queue & Update
    // ===================================
    protected readonly ConcurrentQueue<Action> _q = new();
    protected int _pumping = 0;

    // [ping-fix] PumpQueuedActions 스킵 카운터. 어느 tick 에 contention 으로 건너뛴어졌는지 도중 집계.
    // 어딘가에서 보고성 선택적으로 조회할 수 있도록 internal 으로 노출.
    private long _pumpSkipCount;
    public long PumpSkipCount => Interlocked.Read(ref _pumpSkipCount);

    protected void Enqueue(Action a) => _q.Enqueue(a);

    public void PumpQueuedActions()
    {
        if (Interlocked.Exchange(ref _pumping, 1) == 1)
        {
            // [ping-fix] Console.WriteLine ❘ hot-path 제거. Lock contention 은 드물지 않아서
            // 매 tick 마다 찍히면 stdout 동기 I/O 로 핵심 퀴런 자체가 맀린다.
            // LogManager (비동기 큐) 도 너무 자주 부르면 드롭되므로, 할 일이 스킵된 것에 대한 집계만 남김.
            Interlocked.Increment(ref _pumpSkipCount);
            return;
        }
        try { while (_q.TryDequeue(out var a)) a(); }
        finally { _pumping = 0; }
    }

    public virtual void Update()
    {
        PumpQueuedActions();
        if (IsRoomRunning())
            GetSession()?.Update();
    }

    protected void LogRoomBind(ClientSession s, int actorId, int seat, bool isNew, int playerCount)
    {
        var action = isNew ? "join" : "reattach";
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=room_bind action={action} roomType={RoomLogKind} world={RoomLogWorld()} uid={SessionUid(s)} epoch={s.Epoch} conn={SessionConn(s)} actor={actorId} seat={seat} players={PlayerCountText(playerCount)} key={SessionKey(s)}");
    }

    protected void LogRoomBindFail(ClientSession? s, string reason, int playerCount = -1)
    {
        LogManager.Instance.LogWarning(
            "SessionLifecycle",
            $"event=room_bind_fail reason={reason} roomType={RoomLogKind} world={RoomLogWorld()} uid={SessionUid(s)} epoch={SessionEpoch(s)} conn={SessionConn(s)} actor={SessionActor(s)} seat={SessionSeat(s)} players={PlayerCountText(playerCount)} key={SessionKey(s)}");
    }

    private void LogRoomDetach(string uid, long epoch, string connId, int actorId, int seat, int playerCount)
    {
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=room_detach reason=disconnect roomType={RoomLogKind} world={RoomLogWorld()} uid={UidOrDash(uid)} epoch={epoch} conn={ConnOrDash(connId)} actor={actorId} seat={seat} players={PlayerCountText(playerCount)}");
    }

    private void LogRoomDetachSkip(string uid, long epoch, string connId, string reason, int actorId, int seat, string currentConnId)
    {
        LogManager.Instance.LogWarning(
            "SessionLifecycle",
            $"event=room_detach_skip reason={reason} roomType={RoomLogKind} world={RoomLogWorld()} uid={UidOrDash(uid)} epoch={epoch} conn={ConnOrDash(connId)} currentConn={ConnOrDash(currentConnId)} actor={actorId} seat={seat}");
    }

    private void LogRoomRemove(string uid, long epoch, string connId, int actorId, int seat, int playerCount)
    {
        LogManager.Instance.LogInfo(
            "SessionLifecycle",
            $"event=room_remove_player roomType={RoomLogKind} world={RoomLogWorld()} uid={UidOrDash(uid)} epoch={epoch} conn={ConnOrDash(connId)} actor={actorId} seat={seat} players={PlayerCountText(playerCount)}");
    }

    private void LogRoomRemoveSkip(string uid, long epoch, string reason)
    {
        LogManager.Instance.LogWarning(
            "SessionLifecycle",
            $"event=room_remove_skip reason={reason} roomType={RoomLogKind} world={RoomLogWorld()} uid={UidOrDash(uid)} epoch={epoch}");
    }

    protected string RoomLogWorld() => string.IsNullOrWhiteSpace(RoomLogId) ? "-" : RoomLogId;
    protected string PlayerCountText(int playerCount) => playerCount < 0 ? $"?/{_maxPlayers}" : $"{playerCount}/{_maxPlayers}";
    private static string SessionUid(ClientSession? s) => UidOrDash(s?.Uid);
    private static string SessionConn(ClientSession? s) => ConnOrDash(s?.ConnId);
    private static string SessionKey(ClientSession? s) => string.IsNullOrWhiteSpace(s?.Key) ? "-" : s.Key;
    private static long SessionEpoch(ClientSession? s) => s?.Epoch ?? 0;
    private static int SessionActor(ClientSession? s) => s?.ActorId ?? -1;
    private static int SessionSeat(ClientSession? s) => s?.SeatIndex ?? -1;
    private static string UidOrDash(string? uid) => string.IsNullOrWhiteSpace(uid) ? "-" : uid;
    private static string ConnOrDash(string? connId) => string.IsNullOrWhiteSpace(connId) ? "-" : connId;
}
