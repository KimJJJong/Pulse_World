using GameServer.Content.Map;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using Util;

public sealed class TownRoom : RoomBase
{
    public string TownId { get; }

    enum RoomPhase { Waiting, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    private readonly ILogger _logger;

    private TownSession? _session;
    private RhythmSystem? _rhythm;
    private RhythmConfig? _rhythmConfig;
    private Map2D? _map;

    public TownRoom(string townId, int maxPlayers = 64, ILogger? logger = null)
        : base(maxPlayers, 10) // Start ActorId = 10
    {
        TownId = townId;
        _logger = logger ?? NullLogger.Instance;

        StartTownIfNeeded();
    }

    protected override SessionBase? GetSession() => _session;
    protected override bool IsRoomRunning() => _phase == RoomPhase.Running;
    protected override bool CheckRoomEnded() => _phase == RoomPhase.Ended;

    protected override void UpdateSessionWorldId(ClientSession s)
    {
        s.CurrentWorldId = TownId;
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

        _map = MapDatabase.Get("Town_01");

        _rhythmConfig = new RhythmConfig
        {
            Bpm = 240,
            BaseBeatDivision = 1,
            ActionWindowMs = 100,
            MaxBeatLookAhead = 2,
        };

        long songStart = AppRef.ServerTimeMs();

        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart);
        _session = new TownSession(
            sessionId: 0,
            time: time,
            broadcaster: this,
            rhythm: _rhythm,
            rhythmConfig: _rhythmConfig,
            map: _map);

        _rhythm.OnBeat += _session.OnBeat;

        // 월드에 현재 논리 플레이어(연결 유무 상관없이)로 엔티티 빌드
        var players = BuildPlayerEntities();
        _session.InitTown(players);

        foreach (var cs in GetBroadcastSnapshot())
            _session.SendInitPacketToPlayer(cs);

        Broadcast(new SC_BeatSync
        {
            ServerSendTimeMs = AppRef.ServerTimeMs(),
            SongStartServerTimeMs = songStart,
            Bpm = _rhythmConfig.Bpm,
            BaseBeatDivision = _rhythmConfig.BaseBeatDivision,
            BeatIndex = _rhythm.GetCurrentBeatIndex(time.NowMs),
        });

        _logger.LogInformation("TownRoom {TownId} started", TownId);
    }

    private List<MapEntity> BuildPlayerEntities()
    {
        var players = new List<MapEntity>();
        if (_map == null) return players;
        lock (_lock)
        {
            foreach (var p in _players.Values)
            {
                var spawnSet = _map.GetSpawnPointRandom(); 
                var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);

                var e = new MapEntity(
                    id: p.ActorId, 
                    type: EntityType.Player,
                    initialPos: spawn
                );

                e.SetState("HP", 100);
                e.SetState("Uid", p.Uid);

                players.Add(e);
            }
        }
        return players;
    }

    protected override void OnNewPlayerJoinedQueue(RoomPlayer p, SessionBase session)
    {
        // Town Specific: Create Entity and Spawn immediately
        if (_map == null) return;

        var spawnSet = _map.GetSpawnPointRandom();
        var spawn = new GridPos(spawnSet.Item1, spawnSet.Item2);

        var e = new MapEntity(
            id: p.ActorId,
            type: EntityType.Player,
            initialPos: spawn
        );

        e.SetState("HP", 100);
        e.SetState("Uid", p.Uid);

        // Cast to TownSession to call OnPlayerJoined
        ((TownSession)session).OnPlayerJoined(e);
    }

    protected override void MaybeEndIfEmpty()
    {
        bool empty;
        lock (_lock) empty = _players.Count == 0;

        if (!empty) return;

        Console.WriteLine($"[RoomEnd] Town {TownId} is empty but keeping open for now.");
        // lock (_lock) _phase = RoomPhase.Ended;
        // TownManager.Remove(TownId);
    }

    public override void Update()
    {
        if (_phase != RoomPhase.Running) return;
        base.Update();
        _rhythm?.Update();
    }

    // ===========================
    // Packet Routing
    // ===========================
    //public void OnCS_ActionRequest(ClientSession s, CS_ActionRequest p)
    //    => Enqueue(() =>
    //    {
    //        if (!ValidateSessionAction(s, out int actorId)) return;
    //        _session?.OnClientActionPacketByActorId(actorId, p);
    //    });

    public void OnCS_TownActionRequest(ClientSession s, CS_TownActionRequest p)
        => Enqueue(() =>
        {
            if (!ValidateSessionAction(s, out int actorId)) return;
            _session?.OnClientActionPacketByActorId(actorId, p);
        });

    private bool ValidateSessionAction(ClientSession s, out int actorId)
    {
        actorId = -1;
        if (_phase != RoomPhase.Running || _session == null || s == null || !s.HasAuth)
        {
            s.Send(new SC_Warn { code = 3001, msg = "TOWN_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
            return false;
        }

        actorId = s.ActorId;
        if (actorId < 0)
        {
            s.Send(new SC_Warn { code = 3003, msg = "UNKNOWN_ACTOR" }.Write());
            return false;
        }

        // Connection validation
        lock (_lock)
        {
            if (!_byActor.TryGetValue(actorId, out var cur) || !ReferenceEquals(cur, s))
            {
                s.Send(new SC_Warn { code = 3004, msg = "NOT_CURRENT_CONNECTION" }.Write());
                return false;
            }
        }
        return true;
    }

    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }
}
