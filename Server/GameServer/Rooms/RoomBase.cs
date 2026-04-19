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
        if (s == null || !s.HasAuth || string.IsNullOrEmpty(s.Uid))
            return false;

        bool isNew = false;
        int seat = -1;
        RoomPlayer? p = null;

        lock (_lock)
        {
            if (CheckRoomEnded()) return false;

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
            }
            else
            {
                // New Join
                if (_players.Count >= _maxPlayers) return false;
                if (_freeSeats.Count == 0 && _players.Count < _maxPlayers)
                {
                    // Should not happen if logic is correct, but safety
                    // [ping-fix] Console.WriteLine → LogManager
                    LogManager.Instance.LogError("RoomBase",
                        $"FreeSeats empty but room not full. players={_players.Count} max={_maxPlayers}");
                    return false;
                }
                if (_freeSeats.Count == 0) return false;

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
            }
        }

        OnPlayerBound(p, isNew);
        return true;
    }

    public virtual void DetachIfMatch(string uid, long epoch, string connId)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
                return;

            var cur = p.Conn;
            if (cur == null || cur.ConnId != connId)
                return;

            _byActor[p.ActorId] = null;
            p.Detach();

            _broadcastDirty = true;
        }
    }

    public virtual void RemovePlayer(string uid, long epoch)
    {
        lock (_lock)
        {
            if (!_players.TryGetValue((uid, epoch), out var p))
                return;

            _players.Remove((uid, epoch));
            _byActor.Remove(p.ActorId);

            _freeSeats.Enqueue(p.SeatIndex);

            _broadcastDirty = true;

            // [ping-fix] Console.WriteLine → LogManager
            LogManager.Instance.LogInfo("RoomBase",
                $"RemovePlayer uid={uid} epoch={epoch} remaining={_players.Count}");

            // Call Session OnPlayerLeft via Queue
            Enqueue(() => GetSession()?.OnPlayerLeft(p.ActorId));
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
        if (!IsRoomRunning()) return;
        PumpQueuedActions();
        GetSession()?.Update();
    }
}
