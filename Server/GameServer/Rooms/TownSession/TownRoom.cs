using Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Util;

using GameServer.Content.Map;
using GameServer.InGame.Manager.Entity;

// TownSession은 네가 앞에서 만들었던 SessionBase/TownSession 구조를 쓰는 걸 전제로 함
// (TownSession 안에서는 CS_ActionRequest의 Move만 처리)
public sealed class TownRoom : IGameBroadcaster, IUpdatable
{
    readonly object _lock = new();

    // slot -> session
    readonly Dictionary<int, ClientSession> _slots = new();

    // broadcast snapshot
    private ClientSession[] _broadcastSnapshot = Array.Empty<ClientSession>();
    private bool _broadcastDirty = true;

    public string TownId { get; }

    enum RoomPhase { Waiting, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    private readonly ILogger _logger;

    // Town 세션(인게임 GameSession과 동일한 “세션” 개념)
    private TownSession _session;
    private Map2D _map;

    // IServerTime 어댑터
    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }

    public TownRoom(string townId, ILogger logger = null)
    {
        TownId = townId;
        _logger = logger ?? NullLogger.Instance;
        _slots.Clear();
        StartTownIfNeeded();
    }

    // =========================
    // Bind / Unbind
    // =========================
    public bool Bind(int slot, ClientSession s)
    {
        lock (_lock)
        {
            if (slot < 0) return false;

            if (_slots.TryGetValue(slot, out var existing) && existing != null && !ReferenceEquals(existing, s))
                return false;

            _slots[slot] = s;
            s.Slot = slot;

            _broadcastDirty = true;
        }

        // 최초 1회 Town 시작
        StartTownIfNeeded();

        // 새로 들어온 플레이어에게 InitPacket 보내기 (세션 준비 후)
        Enqueue(() =>
        {
            if (_phase != RoomPhase.Running || _session == null) return;
            _session.SendInitPacketToPlayer(s);
        });

        return true;
    }

    public void Unbind(ClientSession s)
    {
        lock (_lock)
        {
            var slotsToRemove = _slots
                .Where(kv => ReferenceEquals(kv.Value, s))
                .Select(kv => kv.Key)
                .ToArray();

            foreach (var slot in slotsToRemove)
            {
                _session?.OnPlayerLeft(slot);
                _slots[slot] = null;
            }

            _broadcastDirty = true;

            GetBroadcastSnapshot();

            if (_broadcastSnapshot.Length <= 0)
            {
                _phase = RoomPhase.Ended;
                TownManager.Remove(TownId);
            }
        }
    }

    // =========================
    // Broadcaster
    // =========================
    private void Broadcast(ArraySegment<byte> payload)
    {
        var targets = GetBroadcastSnapshot();
        foreach (var t in targets)
            t.Send(payload);
    }

    public void Broadcast(IPacket pkt) => Broadcast(pkt.Write());

    private void SendTo(ClientSession s, ArraySegment<byte> payload) => s?.Send(payload);
    public void SendTo(ClientSession s, IPacket pkt) => SendTo(s, pkt.Write());

    public void SendToSlot(int slot, IPacket pkt)
    {
        ClientSession target;
        lock (_lock) _slots.TryGetValue(slot, out target);
        target?.Send(pkt.Write());
    }

    public ClientSession GetSessionBySlot(int slot)
    {
        lock (_lock) return _slots.TryGetValue(slot, out var s) ? s : null;
    }

    private ClientSession[] GetBroadcastSnapshot()
    {
        lock (_lock)
        {
            if (!_broadcastDirty)
                return _broadcastSnapshot;

            if (_slots.Count == 0)
            {
                _broadcastSnapshot = Array.Empty<ClientSession>();
                _broadcastDirty = false;
                return _broadcastSnapshot;
            }

            var list = new List<ClientSession>(_slots.Count);
            foreach (var s in _slots.Values)
            {
                if (s != null)
                    list.Add(s);
            }

            _broadcastSnapshot = list.ToArray();
            _broadcastDirty = false;
            return _broadcastSnapshot;
        }
    }

    // =========================
    // Start Town
    // =========================
    private void StartTownIfNeeded()
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Running || _phase == RoomPhase.Ended)
                return;

            _phase = RoomPhase.Running;
        }

        // 맵 로드 (TownMap으로 교체)
        _map = MapDatabase.Get("Twon_01"); // TODO: 실제 town map key로

        var time = new ServerTimeAdapter();

        // TownSession 구성
        _session = new TownSession(
            sessionId: 0,
            time: time,
            broadcaster: this,
            map: _map
        );

        // 현재 바인딩된 유저들을 엔티티로 빌드하여 Init
        var players = BuildPlayerEntities();
        _session.InitTown(players);

        // 이미 접속해 있던 사람들에게 init packet 전송
        foreach (var cs in GetBroadcastSnapshot())
            _session.SendInitPacketToPlayer(cs);

        _logger.LogInformation("TownRoom {TownId} started", TownId);
    }

    private List<MapEntity> BuildPlayerEntities()
    {
        var players = new List<MapEntity>();

        lock (_lock)
        {
            foreach (var kv in _slots)
            {
                int slot = kv.Key;
                ClientSession s = kv.Value;
                if (s == null || string.IsNullOrEmpty(s.Uid)) continue;

                // 스폰 위치: slot 기준
                var spawnSet = _map.GetSpawnPoint(slot);
                var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);

                var e = new MapEntity(
                    id: slot, // 프로토 동일: slot == entityId
                    type: EntityType.Player,
                    initialPos: spawn
                );

                // Town에서는 HP 없어도 되지만, 공용 init 패킷을 재사용하면 일단 넣어둠
                e.SetState("HP", 100);
                e.SetState("Slot", slot);
                e.SetState("OwnerSlot", slot);
                e.SetState("Uid", s.Uid!);

                players.Add(e);
            }
        }

        return players;
    }

    // =========================
    // Queue (네트워크 스레드 -> 워커 스레드)
    // =========================
    readonly System.Collections.Concurrent.ConcurrentQueue<Action> _q = new();
    int _pumping = 0;

    private void Enqueue(Action a)
    {
        _q.Enqueue(a);
        // Pump(); -> Work Thread에서 처리
    }

    private void PumpQueuedActions()
    {
        if (Interlocked.Exchange(ref _pumping, 1) == 1) return;
        try { while (_q.TryDequeue(out var a)) a(); }
        finally { _pumping = 0; }
    }

    private bool IsRunnableFor(ClientSession s)
    {
        if (_phase != RoomPhase.Running) return false;
        lock (_lock) return _slots.Values.Contains(s);
    }

    // =========================
    // Update (TownWorker에서 호출)
    // =========================
    public void Update()
    {
        if (_phase != RoomPhase.Running) return;

        PumpQueuedActions();

        // TownSession 내부에서 snapshot 브로드캐스트/정리 등을 Update로 처리
        _session?.Update();
    }

    // =========================
    // Packet Routing: CS_ActionRequest (이동 입력 동일 처리)
    // =========================
    public void OnCS_ActionRequest(ClientSession s, CS_ActionRequest p)
        => Enqueue(() =>
        {
            if (!IsRunnableFor(s))
            {
                s.Send(new SC_Warn { code = 3001, msg = "TOWN_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
                return;
            }

            if (_session == null)
            {
                s.Send(new SC_Warn { code = 3002, msg = "TOWN_SESSION_NOT_READY" }.Write());
                return;
            }

            int slot = s.Slot;
            if (slot < 0)
            {
                s.Send(new SC_Warn { code = 3003, msg = "UNKNOWN_SLOT" }.Write());
                return;
            }

            _session.OnClientActionPacketBySlot(slot, p);
        });

    // (선택) Town 전용 인터랙션 패킷이 생기면 여기 추가:
    // public void OnCS_TownInteract(ClientSession s, CS_TownInteract p) => Enqueue(() => { ... });
}
