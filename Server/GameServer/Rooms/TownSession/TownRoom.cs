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

    // (uid, epoch) -> slot (논리 플레이어)
    readonly Dictionary<(string uid, long epoch), int> _slotByUser = new();

    // slot <-> actorId (엔티티 식별)
    readonly Dictionary<int, int> _actorBySlot = new();
    readonly Dictionary<int, int> _slotByActor = new();

    // grace (연결 끊김 후 잠시 유지)
    readonly Dictionary<(string uid, long epoch), GraceState> _grace = new();


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

    // settings
    readonly int _maxPlayers;
    readonly int _graceMs;

    // allocators
    int _nextActorId = 1000;
    readonly Queue<int> _freeSlots = new();

    // IServerTime 어댑터
    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }

    public TownRoom(string townId, int maxPlayers = 64, int graceMs = 30000, ILogger? logger = null)
    {
        TownId = townId;
        _logger = logger ?? NullLogger.Instance;

        _maxPlayers = Math.Max(1, maxPlayers);
        _graceMs = Math.Max(1000, graceMs);

        // seat allocator init
        for (int i = 0; i < _maxPlayers; i++)
            _freeSlots.Enqueue(i);

        StartTownIfNeeded();
    }

    // =========================
    // Bind / Detach / Remove
    // =========================
    /// <summary>
    /// 정석: CS_MapEnter 시 호출.
    /// - (uid,epoch)가 이미 있으면: slot/actor 유지 + session만 교체(재연결)
    /// - 신규면: seat/actor 할당
    /// </summary>
    public bool BindOrReattach(ClientSession s, out int slot, out int actorId)
    {
        slot = -1;
        actorId = -1;

        if (s == null || !s.HasAuth || string.IsNullOrEmpty(s.Uid))
            return false;

        bool isNew = false;

        lock (_lock)
        {
            if (_phase == RoomPhase.Ended) return false;

            var userKey = (s.Uid, s.Epoch);

            // ----- reattach -----
            if (_slotByUser.TryGetValue(userKey, out var existingSlot))
            {
                slot = existingSlot;
                actorId = _actorBySlot[slot];

                // 기존 연결 있으면 교체(정책: 최신 연결이 승자)
                _slots[slot] = s;
                s.Slot = slot;
                s.CurrentWorldId = TownId;

                // grace 취소
                CancelGrace_NoLock(userKey);

                _broadcastDirty = true;

                // 재연결은 "player left"가 아님. 별도 처리 원하면 TownSession에 OnPlayerReattached 같은 훅 추가
            }
            else
            {
                // ----- new join -----
                if (_slotByUser.Count >= _maxPlayers) return false;
                if (_freeSlots.Count == 0) return false;

                slot = _freeSlots.Dequeue();
                actorId = _nextActorId++;

                _slotByUser[userKey] = slot;
                _actorBySlot[slot] = actorId;
                _slotByActor[actorId] = slot;

                _slots[slot] = s;
                s.Slot = slot;
                s.CurrentWorldId = TownId;

                isNew = true;
                _broadcastDirty = true;
            }
        }

        StartTownIfNeeded();

        // 신규 입장 시엔 기존 init 패킷 재사용(너 구조)
        Enqueue(() =>
        {
            if (_phase != RoomPhase.Running || _session == null) return;
            _session.SendInitPacketToPlayer(s);
        });

        // 신규 입장 시 TownSession에 "입장"을 알리고 싶으면(선택):
        // Enqueue(() => _session?.OnPlayerJoined(slot));

        return true;
    }

    /// <summary>
    /// 연결 끊김(소켓 종료) 시 호출하는 정석 API.
    /// slot은 유지하고, grace 동안 재연결을 기다린다.
    /// </summary>
    public void DetachIfMatch(string uid, long epoch, string connId)
    {
        (string uid, long epoch) userKey = (uid, epoch);
        int slot = -1;
        ClientSession? existing = null;

        lock (_lock)
        {
            if (!_slotByUser.TryGetValue(userKey, out slot))
                return;

            if (!_slots.TryGetValue(slot, out existing) || existing == null)
                return;

            if (existing.ConnId != connId)
                return; // 이미 다른 연결로 교체됨

            // 연결만 떼기
            _slots[slot] = null;
            existing.Slot = slot;          // slot은 유지(재연결 시 재사용)
            existing.CurrentWorldId = "";  // detach

            _broadcastDirty = true;

            // grace 시작 (중복 호출 시 token으로 최신만 유효)
            StartGrace_NoLock(userKey, slot);
        }

        // (선택) TownSession에 "오프라인" 처리 알리고 싶으면:
        // Enqueue(() => _session?.OnPlayerDetached(slot));
    }
    /// <summary>
    /// 진짜 퇴장(로그아웃, 강퇴, grace 만료 등).
    /// slot/actorId도 제거하고 좌석 반환.
    /// </summary>
    public void RemovePlayer(string uid, long epoch, string reason = "leave")
    {
        (string uid, long epoch) userKey = (uid, epoch);

        int slot;
        int actorId;
        ClientSession? sessionAtSlot;

        lock (_lock)
        {
            if (!_slotByUser.TryGetValue(userKey, out slot))
                return;

            actorId = _actorBySlot[slot];
            _slotByUser.Remove(userKey);
            _actorBySlot.Remove(slot);
            _slotByActor.Remove(actorId);

            _slots.TryGetValue(slot, out sessionAtSlot);
            _slots[slot] = null;

            CancelGrace_NoLock(userKey);

            // seat 반환
            _freeSlots.Enqueue(slot);

            _broadcastDirty = true;
        }

        Enqueue(() =>
        {
            // TownSession 내부 정리 훅(기존 유지)
            _session?.OnPlayerLeft(slot);
        });

        // 사람이 아무도 없으면 종료(기존 로직 유지)
        MaybeEndIfEmpty();
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
        if (s == null || !s.HasAuth) return false;

        lock (_lock)
        {
            // 세션이 실제로 해당 slot의 현재 연결인지 확인(재연결/교체 안전)
            if (s.Slot < 0) return false;
            return _slots.TryGetValue(s.Slot, out var cur) && ReferenceEquals(cur, s);
        }
    }

    // =========================
    // Update (TownWorker에서 호출)
    // =========================
    public void Update()
    {
        if (_phase != RoomPhase.Running) return;

        PumpQueuedActions();
        PumpGraceExpirations();

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

    // ==========================================================
    // Grace handling
    // ==========================================================

    sealed class GraceState
    {
        public int Slot;
        public long UntilMs;
        public int Token;
    }

    void StartGrace_NoLock((string uid, long epoch) userKey, int slot)
    {
        var now = NowMs();

        if (!_grace.TryGetValue(userKey, out var st))
        {
            st = new GraceState();
            _grace[userKey] = st;
        }

        st.Slot = slot;
        st.UntilMs = now + _graceMs;
        st.Token++;
    }

    void CancelGrace_NoLock((string uid, long epoch) userKey)
    {
        if (_grace.TryGetValue(userKey, out var st))
            st.Token++; // 기존 grace 무효화
        _grace.Remove(userKey);
    }

    void PumpGraceExpirations()
    {
        List<(string uid, long epoch)> expired = null!;
        var now = NowMs();

        lock (_lock)
        {
            if (_grace.Count == 0) return;

            foreach (var kv in _grace)
            {
                var key = kv.Key;
                var st = kv.Value;

                if (now < st.UntilMs) continue;

                // 아직도 그 slot이 비어있으면(재연결 안 됨) 만료
                if (_slots.TryGetValue(st.Slot, out var cur) && cur == null)
                {
                    expired ??= new List<(string uid, long epoch)>();
                    expired.Add(key);
                }
                else
                {
                    // 재연결됨 -> grace 제거
                    // (재연결은 BindOrReattach에서 CancelGrace 했지만, 안전망)
                    expired ??= new List<(string uid, long epoch)>();
                    expired.Add(key);
                }
            }

            if (expired != null)
            {
                foreach (var key in expired)
                    _grace.Remove(key);
            }
        }

        if (expired == null) return;

        // grace 만료자는 실제 퇴장 처리
        foreach (var key in expired)
        {
            // 재연결된 케이스는 RemovePlayer가 slotByUser가 존재하더라도
            // slots[slot] != null이면 RemovePlayer 하지 않도록 보호하고 싶다면,
            // 여기서 한 번 더 확인하고 호출해도 됨.
            RemovePlayer(key.uid, key.epoch, reason: "grace_expired");
        }
    }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    void MaybeEndIfEmpty()
    {
        bool shouldEnd = false;
        lock (_lock)
        {
            // 논리 플레이어 기준으로 비었는지 판단
            if (_slotByUser.Count <= 0)
                shouldEnd = true;
        }

        if (!shouldEnd) return;

        lock (_lock)
        {
            _phase = RoomPhase.Ended;
        }

        TownManager.Remove(TownId);
    }
}