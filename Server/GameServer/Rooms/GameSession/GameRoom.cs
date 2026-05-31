using GameServer.Content.Map;
using GameServer.InGame.Director.Data;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Shared.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Util;

public sealed class GameRoom : RoomBase
{
    readonly HashSet<string> _loaded = new();

    public string MatchId { get; }
    public string MapId { get; private set; }
    public int Seed { get; private set; }

    enum RoomPhase { Waiting, Countdown, Running, Ended }
    RoomPhase _phase = RoomPhase.Waiting;

    readonly ILogger _logger;

    private GameSession? _session;
    private RhythmSystem? _rhythm;
    private RhythmConfig? _rhythmConfig;
    private Map2D? _map;

    CancellationTokenSource? _startCountdownCts;
    long _plannedStartAtMs = -1;
    long _songStartAtMs = -1;
    long _lastDetachedSweepAtMs;

    const int DetachedSweepIntervalMs = 1000;

    public GameRoom(string matchId, string mapId, ILogger? logger = null, int maxPlayers = 2)
        : base(maxPlayers, 1)
    {
        MatchId = matchId;
        MapId = mapId;
        Seed = Environment.TickCount;
        _logger = logger ?? NullLogger.Instance;
    }

    protected override SessionBase? GetSession() => _session;
    protected override bool IsRoomRunning() => _phase == RoomPhase.Running;
    protected override bool CheckRoomEnded() => _phase == RoomPhase.Ended;
    protected override string RoomLogKind => "Game";
    protected override string RoomLogId => MatchId;

    protected override void UpdateSessionWorldId(ClientSession s)
    {
        s.CurrentWorldId = MatchId;
    }

    protected override void MaybeEndIfEmpty()
    {
        bool empty;
        lock (_lock) empty = _players.Count == 0;

        LogManager.Instance.LogDebug("GameRoom", $"MaybeEndIfEmpty matchId={MatchId} count={_players.Count}");

        if (!empty) return;

        lock (_lock)
        {
            _phase = RoomPhase.Ended;
            _startCountdownCts?.Cancel();
            _startCountdownCts?.Dispose();
            _startCountdownCts = null;
        }

        LogManager.Instance.LogInfo("GameRoom", $"Destroyed (empty) matchId={MatchId}");
        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_end reason=empty roomType=Game world={MatchId} map={MapId}");
        GameManager.Remove(MatchId);
    }

    public bool MarkLoadedAsync(ClientSession s)
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
                return false;

            if (s?.Uid == null)
            {
                LogManager.Instance.LogWarning("GameRoom", "MarkLoadedFail: Session or Uid null");
                return false;
            }

            if (_loaded.Add(s.Uid))
            {
                LogManager.Instance.LogInfo("GameRoom", $"Player loaded uid={s.Uid} loaded={_loaded.Count}/{_players.Count}");
            }

            bool allReady = true;
            foreach (var p in _players.Values)
            {
                if (!_loaded.Contains(p.Uid))
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady && _players.Count >= _maxPlayers)
            {
                LogManager.Instance.LogInfo("GameRoom", $"All players loaded count={_players.Count}");
                return true;
            }

            return false;
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

    protected override void OnPlayerBound(RoomPlayer p, bool isNew)
    {
        if (_phase == RoomPhase.Running)
        {
            base.OnPlayerBound(p, isNew);
            Enqueue(() => SendBeatSyncTo(p.Conn));
            return;
        }

        if (_phase == RoomPhase.Countdown)
        {
            Enqueue(() =>
            {
                if (_session == null || p.Conn == null)
                    return;

                _session.EnsurePlayerSpawned(p.ActorId);
                _session.SendInitPacketToPlayer(p.Conn);
                SendCountdownStateTo(p.Conn);
            });

            ScheduleBroadcastPlayerList();
            return;
        }

        ScheduleBroadcastPlayerList();
    }

    void ScheduleBroadcastPlayerList()
    {
        Enqueue(() =>
        {
            var pkt = new SC_AllPlayersLoaded { matchId = MatchId };
            lock (_lock)
            {
                foreach (var p in _players.Values)
                {
                    pkt.playerss.Add(new SC_AllPlayersLoaded.Players
                    {
                        uid = p.Uid,
                        slot = p.SeatIndex,
                        loaded = _loaded.Contains(p.Uid)
                    });
                }
            }
            Broadcast(pkt);
        });
    }

    protected override void OnNewPlayerJoinedQueue(RoomPlayer p, SessionBase session)
    {
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

        LogManager.Instance.LogWarning("GameRoom", $"Late join attempt uid={p.Uid} actor={p.ActorId}");
    }

    public void BroadcastGameStart(long plannedStartAtMs)
    {
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
            {
                _logger.LogWarning("[GameRoom] BroadcastGameStart ignored. Room already ended. match={MatchId}", MatchId);
                return;
            }

            if (_phase == RoomPhase.Countdown || _phase == RoomPhase.Running)
            {
                _logger.LogInformation(
                    "[GameRoom] BroadcastGameStart ignored. Already started phase={Phase} planned={Planned} song={Song}",
                    _phase,
                    _plannedStartAtMs,
                    _songStartAtMs);
                return;
            }

            _phase = RoomPhase.Countdown;
            _plannedStartAtMs = plannedStartAtMs;
        }

        SetupGameplay(plannedStartAtMs);

        long finalStartMs = _rhythm != null ? _rhythm.SongStartServerTimeMs : plannedStartAtMs;
        lock (_lock)
            _songStartAtMs = finalStartMs;

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

        Broadcast(new SC_GameBegin
        {
            matchId = MatchId,
            startAtMs = finalStartMs,
            startTick = 0
        });

        Broadcast(BuildBeatSyncPacket());
        ScheduleStartTransition(finalStartMs);

        _logger.LogInformation(
            "[GameRoom] BroadcastGameStart planned={Planned} finalSongStart={Final} totalDelay={Delay}",
            plannedStartAtMs,
            finalStartMs,
            finalStartMs - AppRef.ServerTimeMs());
    }

    void SetupGameplay(long startAtMs)
    {
        lock (_lock)
        {
            if (_session != null)
                return;
        }

        _map = MapDatabase.Get(MapId);

        var stageData = StageDataManager.Get(MapId);

        RhythmStageData rhythmData;
        if (ContentStore.Rhythms != null && ContentStore.Rhythms.TryGetValue(MapId, out var parsedRhythm))
        {
            rhythmData = parsedRhythm;
            LogManager.Instance.LogInfo("GameRoom", $"Rhythm data found mapId={MapId} blocks={rhythmData.Blocks?.Count ?? 0}");
        }
        else
        {
            LogManager.Instance.LogWarning("GameRoom", $"Rhythm data not found mapId={MapId}. Creating dummy.");
            rhythmData = new RhythmStageData { StageId = MapId, TicksPerBeat = 480, TimeSignatureNum = 4 };
            rhythmData.Blocks.Add(new RhythmBlock { BlockId = "DummyPhase1", LengthMeasures = 8, DefaultNextBlock = "DummyPhase1" });
        }

        var rhythmManager = new DynamicRhythmManager(rhythmData);
        if (stageData == null)
        {
            _logger.LogError("Stage Data Not Found: {MapId}. Falling back to default Config.", MapId);
            stageData = new StageScenario
            {
                MapId = MapId,
                RhythmSettings = new RhythmSettingsData { Bpm = 120, ActionWindowMs = 100 },
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

        long songStart = startAtMs + rSetting.StartDelayMs;

        var time = new ServerTimeAdapter();
        _rhythm = new RhythmSystem(time, _rhythmConfig, songStart);

        _session = new GameSession(
            sessionId: 0,
            time: time,
            broadcaster: this,
            rhythm: _rhythm,
            rhythmConfig: _rhythmConfig,
            rhythmManager: rhythmManager,
            map: _map
        );

        _rhythm.OnBeat += _session.OnBeat;

        var players = BuildPlayerEntities();
        _session.InitGame(players, stageData);

        foreach (var s in GetBroadcastSnapshot())
            _session.SendInitPacketToPlayer(s);

        _logger.LogInformation(
            "GameRoom {MatchId} pre-scheduled rhythm gameplay. startAt={StartAt} songStart={SongStart} stageDelay={StageDelay}",
            MatchId,
            startAtMs,
            songStart,
            rSetting.StartDelayMs);
    }

    SC_BeatSync BuildBeatSyncPacket()
    {
        var now = AppRef.ServerTimeMs();
        var beatIndex = _rhythm != null ? Math.Max(0, _rhythm.GetCurrentBeatIndex(now)) : 0;

        return new SC_BeatSync
        {
            ServerSendTimeMs = now,
            SongStartServerTimeMs = _songStartAtMs > 0 ? _songStartAtMs : now,
            Bpm = _rhythmConfig?.Bpm ?? 120,
            BaseBeatDivision = _rhythmConfig?.BaseBeatDivision ?? 1,
            BeatIndex = beatIndex
        };
    }

    void SendCountdownStateTo(ClientSession target)
    {
        if (target == null || !target.IsConnected)
            return;

        if (_songStartAtMs <= 0)
            return;

        var begin = new SC_GameBegin
        {
            matchId = MatchId,
            startAtMs = _songStartAtMs,
            startTick = 0
        };

        target.Send(begin.Write());
        SendBeatSyncTo(target);
    }

    void SendBeatSyncTo(ClientSession? target)
    {
        if (target == null || !target.IsConnected)
            return;

        if (_rhythm == null || _rhythmConfig == null || _songStartAtMs <= 0)
            return;

        target.Send(BuildBeatSyncPacket().Write());
    }

    void ScheduleStartTransition(long startAtMs)
    {
        CancellationToken token;
        lock (_lock)
        {
            if (_phase == RoomPhase.Ended)
                return;

            _startCountdownCts?.Cancel();
            _startCountdownCts?.Dispose();
            _startCountdownCts = CancellationTokenSource.CreateLinkedTokenSource(AppRef.Cts.Token);
            token = _startCountdownCts.Token;
        }

        var delay = Math.Max(0, (int)(startAtMs - AppRef.ServerTimeMs()));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token);

                bool enteredRunning = false;
                lock (_lock)
                {
                    if (_phase == RoomPhase.Countdown)
                    {
                        _phase = RoomPhase.Running;
                        enteredRunning = true;
                    }
                }

                if (!enteredRunning)
                    return;

                Broadcast(BuildBeatSyncPacket());
                _logger.LogInformation("GameRoom {MatchId} entered Running at {Now} (songStart={SongStart})", MatchId, AppRef.ServerTimeMs(), _songStartAtMs);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduleStartTransition failed match={MatchId}", MatchId);
            }
        }, token);
    }

    List<MapEntity> BuildPlayerEntities()
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
                ApplyPlayerState(e, p.Uid, 10000);

                players.Add(e);
            }
        }

        return players;
    }

    private static void ApplyPlayerState(MapEntity entity, string uid, int defaultHp)
    {
        if (entity == null)
            return;

        entity.SetState("AppearanceId", 0);

        if (string.IsNullOrWhiteSpace(uid))
            return;

        try
        {
            var pState = ServerServices.ApiClient
                .GetPlayerStateAsync(uid)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (pState == null)
            {
                Console.WriteLine($"[GameRoom] PlayerState missing. uid={uid}");
                return;
            }

            int hp = pState.TotalHp > 0 ? pState.TotalHp : defaultHp;
            entity.SetState("HP", hp);
            entity.SetState("ATK", pState.TotalAtk);
            entity.SetState("DEF", pState.TotalDef);
            entity.SetState("AppearanceId", pState.AppearanceId);

            Console.WriteLine(
                $"[GameRoom] PlayerState loaded. uid={uid} HP={hp} AppearanceId={pState.AppearanceId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameRoom] PlayerState load failed. uid={uid} err={ex.Message}");
        }
    }

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

    bool ValidateSessionAction(ClientSession s, out int actorId)
    {
        actorId = -1;
        if (_phase != RoomPhase.Running || _session == null || !s.HasAuth)
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
        base.Update();

        if (_phase != RoomPhase.Running)
            return;

        CheckDetachedPlayers();
        _rhythm?.Update();
    }

    void CheckDetachedPlayers()
    {
        var now = Environment.TickCount64;
        if (now - _lastDetachedSweepAtMs < DetachedSweepIntervalMs)
            return;

        _lastDetachedSweepAtMs = now;

        List<(string uid, long epoch)>? toRemove = null;

        lock (_lock)
        {
            foreach (var p in _players.Values)
            {
                if (p.Conn == null && p.LastDetachedTime > 0)
                {
                    if (now - p.LastDetachedTime > GameStartTuning.DisconnectGraceMs)
                    {
                        toRemove ??= new List<(string uid, long epoch)>();
                        toRemove.Add((p.Uid, p.Epoch));
                    }
                }
            }
        }

        if (toRemove == null)
            return;

        foreach (var (uid, epoch) in toRemove)
        {
            LogManager.Instance.LogInfo("GameRoom", $"Force removing detached player uid={uid}");
            RemovePlayer(uid, epoch);
        }
    }

    sealed class ServerTimeAdapter : IServerTime
    {
        public long NowMs => AppRef.ServerTimeMs();
    }
}
