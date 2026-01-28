using GameServer.Content.Map;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Util;
using GameServer.InGame.Director.Data; // Added

public sealed class GameRoom : RoomBase
{
    // 로딩 완료 체크(UID 기준)
    readonly HashSet<string> _loaded = new();

    public string MatchId { get; }
    public string MapId { get; private set; }
    public int Seed { get; private set; } = 0;

    enum RoomPhase { Waiting, Loading, Countdown, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    private readonly ILogger _logger;

    // 리듬 + 인게임 세션
    private GameSession? _session;
    private RhythmSystem? _rhythm;
    private RhythmConfig? _rhythmConfig;
    private Map2D? _map;

    private int _nextMonsterId = 100;

    public GameRoom(string matchId, string mapId, ILogger? logger = null, int maxPlayers = 2)
        : base(maxPlayers, 1) // Start ActorId = 1
    {
        MatchId = matchId;
        MapId = mapId;
        Seed = Environment.TickCount;
        _logger = logger ?? NullLogger.Instance;
    }

    protected override SessionBase? GetSession() => _session;
    protected override bool IsRoomRunning() => _phase == RoomPhase.Running;
    protected override bool CheckRoomEnded() => _phase == RoomPhase.Ended;

    protected override void UpdateSessionWorldId(ClientSession s)
    {
        s.CurrentWorldId = MatchId;
    }

    protected override void MaybeEndIfEmpty()
    {
        bool empty;
        lock (_lock) empty = _players.Count == 0;

        Console.WriteLine($"[MaybeEndIfEmpty] MatchId={MatchId} Count={_players.Count}");

        if (!empty) return;

        Console.WriteLine($"[RoomEnd] ==============================================");
        Console.WriteLine($"[RoomEnd] GameRoom {MatchId} DESTROYED. (Empty)");
        Console.WriteLine($"[RoomEnd] ==============================================");
        lock (_lock) _phase = RoomPhase.Ended;
        GameManager.Remove(MatchId);
    }

    // =====================================================
    // Loading gate
    // =====================================================
    public bool MarkLoadedAsync(ClientSession s)
    {
        lock (_lock)
        {
            if (s?.Uid == null)
            {
                Console.WriteLine("[GameRoom] MarkLoadedFail: Session or Uid null");
                return false;
            }

            if (!_loaded.Add(s.Uid))
            {
                // Already loaded
            }
            else
            {
                Console.WriteLine($"[GameRoom] Player Loaded: {s.Uid}. Total Loaded: {_loaded.Count}/{_players.Count}");
            }

            // 현재 방의 논리 플레이어가 전부 로딩 완료인지 체크
            bool allReady = true;
            foreach (var p in _players.Values)
            {
                if (!_loaded.Contains(p.Uid))
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
                Console.WriteLine($"[GameRoom] All Players Loaded! Count={_players.Count}");

            return allReady && _players.Count > 0;
        }
    }

    public IEnumerable<(string uid, int actorId, bool loaded)> GetPlayersSnapshot()
    {
        lock (_lock)
        {
            foreach (var p in _players.Values)
                yield return (p.Uid, p.ActorId, _loaded.Contains(p.Uid));
        }
    }

    // =====================================================
    // Logic overrides
    // =====================================================
    protected override void OnPlayerBound(RoomPlayer p, bool isNew)
    {
        // 게임이 이미 시작된 경우, 재연결/중도 참여 처리
        if (_phase == RoomPhase.Running)
        {
            // Use base logic which enqueues EnsurePlayerSpawned + SendInitPacket
            base.OnPlayerBound(p, isNew);
        }
        else
        {
             // 게임이 아직 시작되지 않음. 바인딩만 하고 BroadcastGameStart에서 Init 패킷 일괄 전송 대기.
             // Don't enqueue Init packet here.
        }
    }

    protected override void OnNewPlayerJoinedQueue(RoomPlayer p, SessionBase session)
    {
        // For GameRoom, entities are usually created at StartGameplay.
        // If "Late Join" is allowed, we create entity here.
        // Assuming NO late join for now in strict sense, but if it happens:
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

        // GameSession InitGame takes a list, but we can't re-init.
        // We should add a specific method to GameSession or just spawn to World.
        // Using base SessionBase spawn logic inside EnsurePlayerSpawned might be enough if added to _players manually?
        // SessionBase.InitPlayers clears _players list. Not good for single add.
        
        // Let's rely on EnsurePlayerSpawned check. If not in _players, we might be in trouble.
        // Since GameRoom creates entities at Start, late joiners (newly created RoomPlayer after Start) won't have entities in _session._players.
        // We need to add them.
        
        // However, RoomBase logic calls OnNewPlayerJoinedQueue only if IsRoomRunning.
        // If running, we assume it's a verify-reconnect or late join.
        // For now, let's assume we just log or try to spawn.
        Console.WriteLine($"[GameRoom] Late join attempt for {p.Uid} (Actor {p.ActorId})");
        
        // Manually adding to session players list would require access. 
        // SessionBase._players is protected.
        // We can cast to specific session if needed or just skip for now as prototype focused on Reconnect.
    }

    // =====================================================
    // Start Logic
    // =====================================================
    public void ScheduleStart(long startAtMs)
    {
        _phase = RoomPhase.Countdown;
        var delay = Math.Max(0, (int)(startAtMs - AppRef.ServerTimeMs()));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, AppRef.Cts.Token);
                StartGameplay();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduleStart failed match={MatchId}", MatchId);
            }
        });
    }

    public void BroadcastGameStart(long startAtMs)
    {
        var loadedPkt = new SC_AllPlayersLoaded { matchId = MatchId };
        lock (_lock)
        {
            foreach (var p in _players.Values)
            {
                loadedPkt.playerss.Add(new SC_AllPlayersLoaded.Players
                {
                    uid = p.Uid,
                    slot = p.SeatIndex,
                    loaded = true
                });
            }
        }
        Broadcast(loadedPkt);

        ScheduleStart(startAtMs);

        Broadcast(new SC_GameBegin
        {
            matchId = MatchId,
            startAtMs = startAtMs,
            startTick = 0
        });
    }

    private void StartGameplay()
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Running || _phase == RoomPhase.Ended)
                return;
            _phase = RoomPhase.Running;
        }

        _map = MapDatabase.Get(MapId);

        // NEW: Load Data from ContentStore (Real)
        var stageData = StageDataManager.Get(MapId);
        if (stageData == null)
        {
            _logger.LogError("Stage Data Not Found: {MapId}. Falling back to default Config.", MapId);
            stageData = new StageScenario 
            { 
                MapId = MapId, 
                RhythmSettings = new RhythmSettingsData { Bpm=120, ActionWindowMs=100 },
                InitialSpawns = new List<SpawnData>()
            };
        }

        var rSetting = stageData.RhythmSettings;

        _rhythmConfig = new RhythmConfig
        {
            Bpm = rSetting.Bpm,
            BaseBeatDivision = rSetting.BaseBeatDivision,
            ActionWindowMs = rSetting.ActionWindowMs,
            MaxBeatLookAhead = 2,
        };

        long songStart = AppRef.ServerTimeMs() + rSetting.StartDelayMs;

        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart);

        _session = new GameSession(
            sessionId: 0,
            time: time,
            broadcaster: this,
            rhythm: _rhythm,
            rhythmConfig: _rhythmConfig,
            map: _map
        );

        _rhythm.OnBeat += _session.OnBeat;

        var players = BuildPlayerEntities();
        
        // OLD: Monsters list pass
        // var monsters = BuildMonsterEntitiesForPrototype();
        // _session.InitGame(players, monsters);

        // NEW: Pass Scenario
        _session.InitGame(players, stageData);

        foreach (var s in GetBroadcastSnapshot())
            _session.SendInitPacketToPlayer(s);

        Broadcast(new SC_BeatSync
        {
            ServerSendTimeMs = AppRef.ServerTimeMs(),
            SongStartServerTimeMs = songStart,
            Bpm = _rhythmConfig.Bpm,
            BaseBeatDivision = _rhythmConfig.BaseBeatDivision,
            BeatIndex = _rhythm.GetCurrentBeatIndex(time.NowMs),
        });

        _logger.LogInformation("GameRoom {MatchId} started rhythm gameplay", MatchId);
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

                e.SetState("HP", 10000);
                e.SetState("Uid", p.Uid);

                players.Add(e);
            }
        }

        return players;
    }

    // Mock Loader Removed
    // private StageScenario LoadStageData(string mapId) ...

    // OLD Monster Builder Removed
    // private List<MapEntity> BuildMonsterEntitiesForPrototype() ...

    // =====================================================
    // Packet Routing
    // =====================================================
    public void OnCS_ActionRequest(ClientSession s, CS_ActionRequest p)
        => Enqueue(() =>
        {
            if (!ValidateSessionAction(s, out int actorId)) return;
            _session?.OnClientActionPacketByActorId(actorId, p);
        });

    public void OnCS_CalibHit(ClientSession s, CS_CalibHit p)
        => Enqueue(() =>
        {
            if (!ValidateSessionAction(s, out int actorId)) return;
            _session?.OnClientCalibPacketByActorId(actorId, p);
        });

    private bool ValidateSessionAction(ClientSession s, out int actorId)
    {
        actorId = -1;
        if (_phase != RoomPhase.Running || _session == null || s == null || !s.HasAuth)
        {
            s.Send(new SC_Warn { code = 2001, msg = "ROOM_NOT_RUNNING_OR_NOT_MEMBER" }.Write());
            return false;
        }

        actorId = s.ActorId;
        if (actorId < 0)
        {
            s.Send(new SC_Warn { code = 2003, msg = "UNKNOWN_ACTOR" }.Write());
            return false;
        }

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

    public ClientSession? GetSessionByActor(int actorId)
    {
        lock (_lock) return _byActor.TryGetValue(actorId, out var s) ? s : null;
    }

    public void SendToActor(int actorId, IPacket pkt)
    {
        ClientSession? target;
        lock (_lock) _byActor.TryGetValue(actorId, out target);
        target?.Send(pkt.Write());
    }

    public override void Update()
    {
        if (_phase != RoomPhase.Running) return;
        base.Update();
        
        CheckDetachedPlayers();

        _rhythm?.Update();
    }

    private void CheckDetachedPlayers()
    {
        // 1초에 한 번 정도만 체크해도 충분 (최적화)
        if (Environment.TickCount64 % 1000 < 50) 
        {
            var now = Environment.TickCount64;
            List<(string, long)> toRemove = null;

            lock (_lock)
            {
                foreach (var p in _players.Values)
                {
                    if (p.Conn == null && p.LastDetachedTime > 0)
                    {
                        // 30초 이상 연결 끊김 상태면 강제 퇴장
                        if (now - p.LastDetachedTime > 30_000)
                        {
                            if (toRemove == null) toRemove = new List<(string, long)>();
                            toRemove.Add((p.Uid, p.Epoch));
                        }
                    }
                }
            }

            if (toRemove != null)
            {
                foreach (var (uid, epoch) in toRemove)
                {
                    Console.WriteLine($"[GameRoom] Force Removing Detached Player: {uid}");
                    RemovePlayer(uid, epoch);
                }
            }
        }
    }

    private sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }
}
