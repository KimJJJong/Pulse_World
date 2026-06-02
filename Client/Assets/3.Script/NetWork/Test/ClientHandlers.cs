using ServerCore;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using System.Collections.Generic;
using Client.Content.Item;

public class ClientHandlers : MonoBehaviour
{
    public static ClientHandlers Instance { get; private set; }
    public ClientGameState GS => ClientGameState.Instance;

    public RhythmClient Rhythm => RhythmClient.Instance;

    public static event System.Action OnBeatSyncReady;


    // [REMOVED] Local tracking is now centralized in BoardView
    private long _lastTelegraphCleanupBeat = long.MinValue;
    private bool _initMapApplied;
    private Coroutine _applyInitMapCoroutine;
    private readonly List<PendingEntitySpawn> _pendingEntitySpawnsBeforeInit = new();

    private BoardView BV => BoardView.Instance;
    void Awake()
    {
        Instance = this;
        _initMapApplied = false;
        _pendingEntitySpawnsBeforeInit.Clear();
    }

    private readonly struct PendingEntitySpawn
    {
        public readonly long BeatIndex;
        public readonly int EntityId;
        public readonly int EntityType;
        public readonly int AppearanceId;
        public readonly int X;
        public readonly int Y;
        public readonly int Hp;

        public PendingEntitySpawn(long beatIndex, int entityId, int entityType, int appearanceId, int x, int y, int hp)
        {
            BeatIndex = beatIndex;
            EntityId = entityId;
            EntityType = entityType;
            AppearanceId = appearanceId;
            X = x;
            Y = y;
            Hp = hp;
        }

        public static PendingEntitySpawn From(SC_EntitySpawn p)
            => new PendingEntitySpawn(p.BeatIndex, p.EntityId, p.EntityType, p.AppearanceId, p.X, p.Y, p.Hp);

        public ClientEntityInfo ToEntityInfo()
            => new ClientEntityInfo
            {
                EntityId = EntityId,
                EntityType = EntityType,
                AppearanceId = AppearanceId,
                X = X,
                Y = Y,
                Hp = Hp
            };
    }


    public void HandleSC_InitMap(SC_InitMap p)
    {
        _initMapApplied = false;

        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[ClientHandlers] HandleSC_InitMap: MapId={p.MapId} MyActorId={p.MyActorId}");

        // 1) 맵 생성
        var mapName = p.MapId;

        MapJson serverMapJson = null;
        MapAsset mapAsset = null;

        if (P2PServerContentResolver.TryLoadMapJson(mapName, p.Mode, out var loadedMapJson))
        {
            serverMapJson = loadedMapJson;

            if (p.MapWidth > 0 && p.MapHeight > 0 &&
                (serverMapJson.width != p.MapWidth || serverMapJson.height != p.MapHeight))
            {
                Debug.LogWarning(
                    $"[InitMap] Server map json size mismatch for {mapName}. Packet=({p.MapWidth}x{p.MapHeight}) Json=({serverMapJson.width}x{serverMapJson.height})");
            }

            if (P2PDebugConfig.TraceCombat)
                Debug.Log($"[InitMap] Using server map json for {mapName}");
        }
        else
        {
            var reg = MapRegistry.EnsureInstance();
            if (reg == null)
            {
                Debug.LogError("[InitMap] MapRegistry.Instance is null. MapRegistry가 씬에 배치되어 있어야 합니다.");
                return;
            }

            if (!reg.TryGet(mapName, out mapAsset) || mapAsset == null)
            {
                Debug.LogError($"[InitMap] MapAsset not found: {mapName}");
                return;
            }

            if (p.MapWidth > 0 && p.MapHeight > 0 &&
                (mapAsset.Width != p.MapWidth || mapAsset.Height != p.MapHeight))
            {
                if (P2PDebugConfig.TraceCombat)
                {
                    Debug.LogWarning(
                        $"[InitMap] Map size mismatch for {mapName}. Packet=({p.MapWidth}x{p.MapHeight}) Asset=({mapAsset.Width}x{mapAsset.Height})");
                }
            }
        }

        // [InitMap_Warn] Ping/Pong 워밍업이 완료되지 않은 채 OnBeatSync가 호출되면
        // TimeSync.OffsetMs가 아직 0 또는 초기값이라 ServerSongStartMs와 시간 축이 어긋난다 (Root Cause A).
        // 원격에서 "Warning이 처음 몇 초 동안만 깜빡이다 안정화"되는 현상의 원인이 이것인지 확인용.
        if (TimeSync.EstimatedRttMs <= 0)
        {
            if (P2PDebugConfig.TraceCombat)
            {
                Debug.LogWarning($"[InitMap_Warn] Ping/Pong warmup not ready! OffsetMs={TimeSync.OffsetMs:F0}, " +
                                 $"EstimatedRttMs={TimeSync.EstimatedRttMs:F0}. SongStart sync may drift by RTT/2 until next Pong.");
            }
        }

        // Rhythm 동기화 (Town에서도 BGM 싱크 등을 위해 사용 가능)
        if (RhythmClient.Instance != null)
        {
            RhythmClient.Instance.judgeWindowMs = (float)p.ActionWindowMs;

            if (p.ServerTimeMs > 0)
                TimeSync.Reset();

            if (RhythmSyncCoordinator.TryApplyBeatSync(
                rhythm: RhythmClient.Instance,
                serverSendTimeMs: p.ServerTimeMs,
                songStartServerTimeMs: p.SongStartServerTime,
                bpm: p.Bpm,
                baseBeatDivision: p.BaseBeatDivision,
                beatIndex: 0,
                sourceTag: "InitMap"))
            {
                OnBeatSyncReady?.Invoke();
                if (P2PDebugConfig.TraceCombat)
                    Debug.Log($"[InitMap] Rhythm Sync: Bpm={p.Bpm}, SongStart={p.SongStartServerTime}");
            }
        }

        if (serverMapJson != null)
            GS.StartMapGeneration(serverMapJson);
        else
            GS.StartMapGeneration(mapAsset);

        if (_applyInitMapCoroutine != null)
            StopCoroutine(_applyInitMapCoroutine);

        _applyInitMapCoroutine = StartCoroutine(CoApplyInitMapAfterMapReady(p, mapName));
    }

    private IEnumerator CoApplyInitMapAfterMapReady(SC_InitMap p, string mapName)
    {
        const float MapReadyApplyTimeoutSeconds = 12f;
        var startTime = Time.time;

        while (GS != null && !GS.IsMapGenerationComplete)
        {
            if (Time.time - startTime > MapReadyApplyTimeoutSeconds)
            {
                Debug.LogWarning(
                    $"[InitMap] Map generation wait timed out before applying entities. map={mapName} progress={GS.MapGenProgress:0.00}");
                break;
            }

            yield return null;
        }

        ApplyInitMapRuntimeState(p, mapName);
        _applyInitMapCoroutine = null;
    }

    private void ApplyInitMapRuntimeState(SC_InitMap p, string mapName)
    {
        var players = p.playerss ?? new List<SC_InitMap.Players>();
        var entities = p.entitiess ?? new List<SC_InitMap.Entities>();

        // 2) 플레이어 Actor 정보
        ApplyRosterSnapshot(players.Select(pa => (pa.ActorId, pa.Uid)));
        GS.SetMyActorId(p.MyActorId);
        LogInitMapPlayerSnapshot(p);
        string myUid = GS.TryGetPlayerUid(p.MyActorId, out var resolvedMyUid) ? resolvedMyUid : "-";
        Debug.Log(
            $"[P2PPlayerSync] InitMap identity sessionUid={SessionContext.Instance?.Uid ?? "-"} myActor={p.MyActorId} rosterUid={myUid} " +
            $"manifestHostUid={SessionContext.Instance?.LastMatchManifest?.HostUid ?? "-"}");
        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[InitMap] MyActorId: {GS.MyActorId}, TotalEntities: {entities.Count}");

        // 3) 엔티티 스폰
        GS.ClearEntities();
        foreach (var e in entities)
        {
            if (P2PDebugConfig.TraceCombat)
                Debug.Log($"Spawn Entity: ID={e.EntityId} Type={(EntityType)e.EntityType} Pos=({e.X},{e.Y}) HP={e.Hp}");
            GS.SpawnOrUpdateEntity(new ClientEntityInfo
            {
                EntityId = e.EntityId,
                EntityType = e.EntityType,
                AppearanceId = e.AppearanceId,
                X = e.X,
                Y = e.Y,
                Hp = e.Hp
            });
        }

        GS.OnInitGameCompleted();
        _initMapApplied = true;
        DrainPendingEntitySpawnsBeforeInit();
        ValidatePlayerEntitySync("InitMap");
        Debug.Log($"[P2PPlayerSync] InitMap applied statePlayers={FormatStatePlayerEntities()}");

        // [HostInit_Diag] InitMap 적용 직후 검증 — Host측 ClientGameState의 신뢰성을 단언한다.
        // 핵심: 본인의 Player Entity가 entitiess 배열에 포함되어 있는지가 Host 권한 활성의 필수 조건.
        // 누락 시 P2PHostController.HandleActionRequest 가 ActorNotFound 로 본인 입력을 영구 drop하고
        // P2PRelayClientBridge.GetHostAuthorityState 가 "LocalPlayerEntityMissing" 으로 떨어진다.
        {
            int my = p.MyActorId;
            bool myInRoster = false;
            if (p.playerss != null)
            {
                for (int i = 0; i < p.playerss.Count; i++)
                {
                    if (p.playerss[i] != null && p.playerss[i].ActorId == my) { myInRoster = true; break; }
                }
            }
            bool myInEntities = false;
            int playerEntityCount = 0;
            if (p.entitiess != null)
            {
                for (int i = 0; i < p.entitiess.Count; i++)
                {
                    var e = p.entitiess[i];
                    if (e == null) continue;
                    if (e.EntityType == (int)EntityType.Player) playerEntityCount++;
                    if (e.EntityType == (int)EntityType.Player && e.EntityId == my) myInEntities = true;
                }
            }

            Debug.Log(
                $"[HostInit_Diag] InitMap.Verify map={p.MapId} myActor={my} myInRoster={myInRoster} " +
                $"myInEntities={myInEntities} playerEntityCount={playerEntityCount} " +
                $"sessionUid={SessionContext.Instance?.Uid ?? "-"} sessionActor={SessionContext.Instance?.MyActorId ?? 0} " +
                $"manifestParticipants={SessionContext.Instance?.LastMatchManifest?.Participants?.Count ?? 0}");

            if (!myInRoster)
                Debug.LogError($"[HostInit_Diag] CRITICAL InitMap roster missing my actor={my}. Host authority will fail (ActorMatchesHostActor=false).");
            if (!myInEntities)
                Debug.LogError(
                    $"[HostInit_Diag] CRITICAL InitMap entitiess missing my Player entity actor={my}. " +
                    $"Symptoms: Host->no own visual, Client->Host can walk through their tile, ActorNotFound drops on input. " +
                    $"Server must include EVERY player participant as EntityType.Player in SC_InitMap.entitiess.");
        }

        if (P2PHostController.HasInstance)
        {
            P2PHostController.Instance.ResetForMatchEnd();
        }

        var relayBridge = P2PRelayClientBridge.Instance;
        relayBridge?.SyncHostState();

        if (relayBridge != null && relayBridge.IsRelayMode && !relayBridge.IsTownRelayMode)
        {
            P2PContentDirector.Instance?.ConfigureStage(mapName);
        }
        else if (P2PContentDirector.HasInstance)
        {
            P2PContentDirector.Instance.ResetMatchState();
        }
    }

    public void Handle_SC_CalibResult(SC_CalibResult p)
    {
        BeatDebugUI_TMP.Instance?.RecordServerDiff(p.DiffMs, p.BeatIndex, RhythmClient.Instance.GetCurrentServerTimeMs());
        AudioOffsetAutoCalibrator.Instance?.OnServerDiff(p.DiffMs);
    }

    public void HandleSC_TownBeatActions(SC_TownBeatActions p)
    {
        foreach (var a in p.beatActionResults)
        {
            var action = new ClientBeatAction
            {
                BeatIndex = p.BeatIndex,
                ActorId = a.ActorId,
                ActionKind = a.ActionKind,
                FromX = a.FromX,
                FromY = a.FromY,
                ToX = a.ToX,
                ToY = a.ToY,
                Rotation = a.Rotation,
                Accepted = a.Accepted
            };

            GS.OnBeatAction(action);
        }
    }

    /// <summary>
    /// 서버 기준 Beat/리듬 동기화
    /// </summary>
    public void Handle_SC_BeatSync(SC_BeatSync p)
    {
        if (P2PDebugConfig.TraceCombat)
            Debug.Log("InHandelbeatSync");
        if (RhythmSyncCoordinator.TryApplyBeatSync(
            rhythm: Rhythm,
            serverSendTimeMs: p.ServerSendTimeMs,
            songStartServerTimeMs: p.SongStartServerTimeMs,
            bpm: p.Bpm,
            baseBeatDivision: p.BaseBeatDivision,
            beatIndex: p.BeatIndex,
            sourceTag: "BeatSync"))
        {
            OnBeatSyncReady?.Invoke();
            if (P2PDebugConfig.TraceCombat)
                Debug.Log($"SongStart={Rhythm.ServerSongStartMs} ServerNow={Rhythm.GetCurrentServerTimeMs()} diff={Rhythm.ServerSongStartMs - Rhythm.GetCurrentServerTimeMs()}ms");
        }
    }

    /// <summary>
    /// 즉각적인 공격/스킬 브로드캐스트 처리 (애니메이션 선행 재생)
    /// </summary>
    public void Handle_SC_ActionInstantBroadcast(SC_ActionInstantBroadcast p)
    {
        if (BV == null) return;

        var bridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
        bool isLocalEcho = bridge != null && bridge.IsDispatchingLocal;

        // 로컬에서 이미 즉시 재생한 액션은 서버 에코만 무시한다.
        if (p.ActorId == GS.MyActorId && !isLocalEcho)
        {
            bridge?.RecordGameplayInstantFeedback(p.ActorId);
            return;
        }

        if (!string.IsNullOrEmpty(p.SkillId))
        {
            BV.PlaySkillInstant(p.ActorId, p.SkillId, p.Rotation, p.StartTick);
        }
        else
        {
            double beatMs = Rhythm.GetBeatDurationMs();
            float duration = (float)(beatMs / 1000.0) * BV.actionDurationRatio;
            BV.PlayInstantActionBroadcast(p.ActorId, (ActionKind)p.ActionKind, p.Rotation, duration);
        }
    }

    /// <summary>
    /// CC에 의한 스킬/액션 취소 브로드캐스트 처리
    /// </summary>
    public void Handle_SC_CancelAction(SC_CancelAction p)
    {
        if (BV == null) return;
    }

    /// <summary>
    /// Beat마다 확정된 액션들
    /// </summary>
    public void Handle_SC_BeatActions(SC_BeatActions p)
    {
        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[ClientHandlers] SC_BeatActions beat={p.BeatIndex} count={p.beatActionResults?.Count ?? 0}");

        if (P2PDebugConfig.LogOverheadEnabled && p.beatActionResults != null)
        {
            var moves = p.beatActionResults
                .Where(x => x != null
                    && x.ActionKind == (int)ActionKind.Move
                    && IsTrackedPlayerActor(x.ActorId))
                .Select(x => $"{x.ActorId}:({x.FromX},{x.FromY})->({x.ToX},{x.ToY}) accepted={x.Accepted}")
                .ToArray();
            if (moves.Length > 0)
                Debug.Log($"[P2PPlayerSync] RecvBeatMoves beat={p.BeatIndex} moves={string.Join(",", moves)}");
        }

        foreach (var a in p.beatActionResults)
        {
            if (P2PDebugConfig.TraceCombat)
                Debug.Log($"[ClientHandlers] BeatAction actor={a.ActorId} kind={(ActionKind)a.ActionKind} from=({a.FromX},{a.FromY}) to=({a.ToX},{a.ToY}) accepted={a.Accepted} hpUpdates={a.hpUpdates?.Count ?? 0}");

            var bridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
            if (bridge != null && !bridge.IsDispatchingLocal && a.ActorId == GS.MyActorId)
            {
                bridge.RecordGameplayBeatResult(a.ActorId, a.ActionKind, a.Accepted, a.FromX, a.FromY, a.ToX, a.ToY);
                P2PTransportDiagnostics.RecordBeatResult(a.ActorId, a.ActionKind, a.Accepted, a.FromX, a.FromY, a.ToX, a.ToY);
            }

            var action = new ClientBeatAction
            {
                BeatIndex = p.BeatIndex,
                ActorId = a.ActorId,
                ActionKind = a.ActionKind,
                FromX = a.FromX,
                FromY = a.FromY,
                ToX = a.ToX,
                ToY = a.ToY,
                Rotation = a.Rotation,
                Accepted = a.Accepted
            };

            // 1) 이동/행동 반영
            GS.OnBeatAction(action);

            if (a.ActorId == GS.MyActorId
                && !a.Accepted
                && (a.ActionKind == (int)ActionKind.Attack || a.ActionKind == (int)ActionKind.Skill)
                && P2PDebugConfig.TraceCombat)
            {
                Debug.Log($"[ClientHandlers] Combat execution rejected after local rhythm input. kind={(ActionKind)a.ActionKind} beat={p.BeatIndex}");
            }

            // 2) HP 업데이트 반영 및 데미지 타이밍 추적
            if (a.hpUpdates != null && a.hpUpdates.Count > 0)
            {
                long clientBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;
                long serverNow  = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentServerTimeMs() : 0;

                if (P2PDebugConfig.TraceCombat)
                {
                    // [DamageRecv] HP 업데이트 수신 — 애니메이션이 시작된 시점 대비 얼마나 늦게 오는지 확인용.
                    long beatGap = clientBeat - p.BeatIndex;
                    Debug.Log($"[DamageRecv] actor={a.ActorId} packetBeat={p.BeatIndex} clientBeat={clientBeat} " +
                              $"beatGap={beatGap} (positive=late) serverNow={serverNow} hpUpdates={a.hpUpdates.Count} " +
                              $"rtt={TimeSync.EstimatedRttMs:F0}ms");
                }

                foreach (var u in a.hpUpdates)
                {
                if (GS.TryGetEntity(u.EntityId, out var info))
                {
                    int oldHp = info.Hp;
                    info.Hp = u.NewHp;
                    if (P2PDebugConfig.TraceCombat)
                        Debug.Log($"[DamageRecv] HP_Change entity={u.EntityId} {oldHp}→{u.NewHp} (delta={u.NewHp - oldHp})");

                    GS.UpdateEntityState(info, refreshWorldView: false);
                    if (info.EntityType == (int)EntityType.Monster)
                        P2PContentDirector.Instance?.MarkWorldDirty();
                }
                else
                {
                    if (P2PDebugConfig.TraceCombat)
                        Debug.LogWarning($"[DamageRecv] Entity not found: {u.EntityId}");
                    }
                }
            }
        }
    }

    public void Handle_SC_BeatTelegraphs(SC_BeatTelegraphs p)
    {
        if (BV == null)
        {
            if (P2PDebugConfig.TraceCombat)
                Debug.LogWarning($"[WarningRecv] BoardView.Instance is null. telegraph render skip. Beat={p.BeatIndex}");
            return;
        }

        long clientBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;

        for (int i = 0; i < p.telegraphss.Count; i++)
        {
            var t = p.telegraphss[i];

            // [Fix-FlickerDedup] 내가 시전 중인 스킬은 로컬 Prediction에서 이미 Warning이 표시되고 있으므로
            // 서버 텔레그래프 패킷을 무시한다. 두 경로가 동시에 SetTelegraphWithExpire를 호출하면
            // 서로 다른 expireBeat 때문에 Warning이 깜빡이는 현상이 발생함 (Root Cause C).
            // IsActorRunningNewSkill: ClientSkillRunner가 해당 actor에 대해 실행 중이면 true.
            if (t.CasterId == GS.MyActorId && BV.IsActorRunningNewSkill(t.CasterId))
            {
                if (P2PDebugConfig.TraceCombat)
                    Debug.Log($"[WarningRecv] SKIP-mine caster={t.CasterId} packetBeat={p.BeatIndex} (local prediction owns this warning)");
                continue;
            }

            if (t.cellss == null || t.cellss.Count == 0)
                continue;

            // [Fix] RTT 보정: p.BeatIndex는 서버가 패킷을 발송한 비트이므로,
            // 원격 환경에서 RTT만큼 늦게 도착하면 이미 과거 비트일 수 있음.
            // clientBeat 기준으로도 expireBeat를 계산해 두 값 중 더 큰 쪽(더 미래)을 사용.
            // → Warning이 항상 최소 durationBeats 만큼 보이도록 보장.
            long durationBeats = (t.DurationTicks + 479) / 480;
            long packetExpire  = p.BeatIndex + durationBeats;
            long clientExpire  = clientBeat  + durationBeats;
            long expireBeat    = System.Math.Max(packetExpire, clientExpire);

            for (int c = 0; c < t.cellss.Count; c++)
            {
                var cell = t.cellss[c];
                BV.SetTelegraphWithExpire(cell.X, cell.Y, expireBeat);

                // [WarningRecv] 서버 경고 수신 로그 — 첫 셀에 대해서만 기록.
                // packetBeat와 clientBeat의 차이(RTT/2 정도)가 크면 원격 지연이 큰 것.
                if (c == 0)
                {
                    if (P2PDebugConfig.TraceCombat)
                    {
                        long beatGap = clientBeat - p.BeatIndex;
                        Debug.Log($"[WarningRecv] caster={t.CasterId} cell=({cell.X},{cell.Y}) " +
                                  $"packetBeat={p.BeatIndex} clientBeat={clientBeat} beatGap={beatGap} " +
                                  $"durationBeats={durationBeats} expireBeat={expireBeat} rtt={TimeSync.EstimatedRttMs:F0}ms");
                    }
                }
            }
        }
    }


    public void Handle_SC_Warn(SC_Warn p)
    {
        if (P2PDebugConfig.TraceCombat)
            Debug.LogWarning($"[SC_Warn] code={p.code} msg={p.msg}");
    }

    public void Handle_SC_EntityDespawn(SC_EntityDespawn p)
    {
        int id = p.EntityId;
        bool removed = GS.RemoveEntity(id);

        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[SC_EntityDespawn] entityId={id} removed={removed}");
        P2PContentDirector.Instance?.OnEntityDespawned(id);

        if (id == GS.MyActorId)
        {
            if (P2PDebugConfig.TraceCombat)
                Debug.LogWarning("[SC_EntityDespawn] My actor despawned. Disable input / show UI.");
        }
    }

    public void Handle_SC_EntitySpawnHandler(SC_EntitySpawn p)
    {
        if (!_initMapApplied)
        {
            BufferEntitySpawnBeforeInit(PendingEntitySpawn.From(p));
            return;
        }

        ApplyEntitySpawn(PendingEntitySpawn.From(p), "SC_EntitySpawn");
    }

    private void BufferEntitySpawnBeforeInit(PendingEntitySpawn spawn)
    {
        for (int i = 0; i < _pendingEntitySpawnsBeforeInit.Count; i++)
        {
            if (_pendingEntitySpawnsBeforeInit[i].EntityId != spawn.EntityId)
                continue;

            _pendingEntitySpawnsBeforeInit[i] = spawn;
            return;
        }

        _pendingEntitySpawnsBeforeInit.Add(spawn);
        Debug.LogWarning(
            $"[SC_EntitySpawn] Buffered before InitMap entity={spawn.EntityId} type={(EntityType)spawn.EntityType} pos=({spawn.X},{spawn.Y})");
        Debug.LogWarning(
            $"[P2PPlayerSync] Buffered spawn before InitMap entity={spawn.EntityId} type={(EntityType)spawn.EntityType} pos=({spawn.X},{spawn.Y})");
    }

    private void DrainPendingEntitySpawnsBeforeInit()
    {
        if (_pendingEntitySpawnsBeforeInit.Count == 0)
            return;

        var pending = _pendingEntitySpawnsBeforeInit.ToArray();
        _pendingEntitySpawnsBeforeInit.Clear();

        for (int i = 0; i < pending.Length; i++)
            ApplyEntitySpawn(pending[i], "BufferedBeforeInitMap");

        Debug.LogWarning($"[ClientHandlers] Replayed {pending.Length} entity spawn(s) received before InitMap.");
        Debug.LogWarning($"[P2PPlayerSync] Replayed buffered spawns count={pending.Length} statePlayers={FormatStatePlayerEntities()}");
    }

    private void ApplyEntitySpawn(PendingEntitySpawn spawn, string source)
    {
        int id = spawn.EntityId;
        bool exists = GS.TryGetEntity(id, out var ent);
        if (exists)
        {
            if (P2PDebugConfig.TraceCombat)
                Debug.LogWarning($"이미 Entity가 존재합니다 ID{id}|| MyId : {GS.MyActorId} source={source}");
            if (spawn.EntityType == (int)EntityType.Player)
            {
                string uid = GS.TryGetPlayerUid(id, out var existingUid) ? existingUid : "-";
                Debug.LogWarning(
                    $"[P2PPlayerSync] Duplicate player spawn ignored source={source} actor={id} uid={uid} " +
                    $"incoming=({spawn.X},{spawn.Y}) existing=({ent.X},{ent.Y}) hp={ent.Hp}");
            }
            return;
        }

        if (spawn.EntityType == (int)EntityType.Player)
            EnsureManifestRosterApplied();

        var entity = spawn.ToEntityInfo();

        GS.SpawnOrUpdateEntity(entity);

        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[SC_EntitySpawn] entityId={entity.EntityId} spawn source={source}");
        P2PContentDirector.Instance?.OnEntitySpawned(entity);

        if (spawn.EntityType == (int)EntityType.Player && P2PRelayClientBridge.HasInstance)
        {
            string uid = GS.TryGetPlayerUid(id, out var playerUid) ? playerUid : "-";
            Debug.Log(
                $"[P2PPlayerSync] Player spawn applied source={source} actor={id} uid={uid} " +
                $"pos=({spawn.X},{spawn.Y}) hp={spawn.Hp} app={spawn.AppearanceId} my={GS.MyActorId}");
            P2PRelayClientBridge.Instance.SyncHostState();
        }

        if (id == GS.MyActorId)
        {
            if (P2PDebugConfig.TraceCombat)
                Debug.LogWarning("[SC_EntitySpawn] My actor overload. Disable / show UI.");
        }
    }

    private void ValidatePlayerEntitySync(string source)
    {
        var rosterActors = new HashSet<int>(GS.PlayerActorIds ?? Array.Empty<int>());
        int missingCount = 0;
        int orphanCount = 0;
        foreach (var actorId in rosterActors)
        {
            if (!GS.TryGetEntity(actorId, out var entity) || entity.EntityType != (int)EntityType.Player)
            {
                missingCount++;
                Debug.LogWarning($"[PlayerSync] Missing player entity after {source}. actor={actorId}");
                Debug.LogWarning($"[P2PPlayerSync] Missing player entity after {source}. actor={actorId}");
            }
        }

        foreach (var entity in GS.EnumerateEntities())
        {
            if (entity.EntityType == (int)EntityType.Player && !rosterActors.Contains(entity.EntityId))
            {
                orphanCount++;
                Debug.LogWarning($"[PlayerSync] Player entity has no roster entry after {source}. actor={entity.EntityId}");
                Debug.LogWarning($"[P2PPlayerSync] Player entity has no roster entry after {source}. actor={entity.EntityId}");
            }
        }

        if (missingCount == 0 && orphanCount == 0)
        {
            Debug.Log(
                $"[P2PPlayerSync] Player entities OK after {source}. " +
                $"roster={FormatRosterActors()} entities={FormatStatePlayerEntities()} my={GS.MyActorId}");
        }
    }

    private void ApplyRosterSnapshot(IEnumerable<(int ActorId, string Uid)> packetRoster)
    {
        var mergedRoster = new Dictionary<int, string>();

        if (packetRoster != null)
        {
            foreach (var (actorId, uid) in packetRoster)
            {
                if (actorId <= 0)
                    continue;

                mergedRoster[actorId] = uid ?? "";
            }
        }

        MergeManifestParticipants(mergedRoster);
        ApplyMergedRoster(mergedRoster);
    }

    private void EnsureManifestRosterApplied()
    {
        var mergedRoster = GS.EnumeratePlayerRoster().ToDictionary(x => x.ActorId, x => x.Uid ?? "");
        int beforeCount = mergedRoster.Count;

        MergeManifestParticipants(mergedRoster);
        if (mergedRoster.Count == beforeCount)
            return;

        ApplyMergedRoster(mergedRoster);
    }

    private void MergeManifestParticipants(Dictionary<int, string> rosterByActorId)
    {
        if (rosterByActorId == null)
            return;

        var participants = SessionContext.Instance.LastMatchManifest?.Participants;
        if (participants == null)
            return;

        foreach (var participant in participants)
        {
            if (participant == null || participant.ActorId <= 0)
                continue;

            if (rosterByActorId.TryGetValue(participant.ActorId, out var existingUid)
                && !string.IsNullOrWhiteSpace(existingUid)
                && !string.Equals(existingUid, participant.Uid ?? "", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[P2PPlayerSync] Manifest roster conflict actor={participant.ActorId} packetUid={existingUid} " +
                    $"manifestUid={participant.Uid ?? "-"}");
            }

            if (!rosterByActorId.ContainsKey(participant.ActorId))
                rosterByActorId[participant.ActorId] = participant.Uid ?? "";
        }
    }

    private void ApplyMergedRoster(Dictionary<int, string> rosterByActorId)
    {
        var orderedRoster = rosterByActorId
            .Where(x => x.Key > 0)
            .OrderBy(x => x.Key)
            .Select(x => (x.Key, x.Value ?? ""))
            .ToArray();

        GS.SetPlayerRoster(orderedRoster);
        GS.SetPlayerActorIds(orderedRoster.Select(x => x.Item1).ToArray());

        Debug.Log($"[P2PPlayerSync] Roster applied count={orderedRoster.Length} actors={FormatRosterActors()}");

        foreach (var duplicateUid in orderedRoster
                     .Where(x => !string.IsNullOrWhiteSpace(x.Item2))
                     .GroupBy(x => x.Item2, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Select(x => x.Item1).Distinct().Count() > 1))
        {
            Debug.LogWarning(
                $"[P2PPlayerSync] Duplicate uid in merged roster uid={duplicateUid.Key} " +
                $"actors={string.Join(",", duplicateUid.Select(x => x.Item1).OrderBy(x => x))} " +
                $"sessionUid={SessionContext.Instance?.Uid ?? "-"}");
        }

        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[ClientHandlers] Roster applied count={orderedRoster.Length} actors={string.Join(",", orderedRoster.Select(x => x.Item1))}");
    }

    private void LogInitMapPlayerSnapshot(SC_InitMap p)
    {
        int totalEntities = p.entitiess?.Count ?? 0;
        int playerEntities = p.entitiess?.Count(e => e.EntityType == (int)EntityType.Player) ?? 0;
        int monsterEntities = p.entitiess?.Count(e => e.EntityType == (int)EntityType.Monster) ?? 0;
        int objectEntities = p.entitiess?.Count(e => e.EntityType == (int)EntityType.Object) ?? 0;

        Debug.Log(
            $"[P2PPlayerSync] InitMap received map={p.MapId} my={p.MyActorId} " +
            $"roster={FormatInitMapRoster(p)} playerEntities={FormatInitMapPlayerEntities(p)} " +
            $"counts total={totalEntities} players={playerEntities} monsters={monsterEntities} objects={objectEntities}");
    }

    private string FormatRosterActors()
        => string.Join(",", GS.EnumeratePlayerRoster()
            .OrderBy(x => x.ActorId)
            .Select(x => $"{x.ActorId}:{(string.IsNullOrWhiteSpace(x.Uid) ? "-" : x.Uid)}"));

    private string FormatStatePlayerEntities()
    {
        var entries = GS.EnumerateEntities()
            .Where(e => e.EntityType == (int)EntityType.Player)
            .OrderBy(e => e.EntityId)
            .Select(e =>
            {
                string uid = GS.TryGetPlayerUid(e.EntityId, out var resolvedUid) && !string.IsNullOrWhiteSpace(resolvedUid)
                    ? resolvedUid
                    : "-";
                return $"{e.EntityId}:{uid}@({e.X},{e.Y}) hp={e.Hp} app={e.AppearanceId}";
            })
            .ToArray();

        return entries.Length == 0 ? "none" : string.Join(",", entries);
    }

    private bool IsTrackedPlayerActor(int actorId)
    {
        if (actorId <= 0)
            return false;

        if (GS.TryGetPlayerUid(actorId, out _))
            return true;

        return GS.PlayerActorIds != null && Array.IndexOf(GS.PlayerActorIds, actorId) >= 0;
    }

    private static string FormatInitMapRoster(SC_InitMap p)
    {
        if (p.playerss == null || p.playerss.Count == 0)
            return "none";

        return string.Join(",", p.playerss
            .OrderBy(x => x.ActorId)
            .Select(x => $"{x.ActorId}:{(string.IsNullOrWhiteSpace(x.Uid) ? "-" : x.Uid)}"));
    }

    private static string FormatInitMapPlayerEntities(SC_InitMap p)
    {
        if (p.entitiess == null || p.entitiess.Count == 0)
            return "none";

        var entries = p.entitiess
            .Where(e => e.EntityType == (int)EntityType.Player)
            .OrderBy(e => e.EntityId)
            .Select(e => $"{e.EntityId}@({e.X},{e.Y}) hp={e.Hp} app={e.AppearanceId}")
            .ToArray();

        return entries.Length == 0 ? "none" : string.Join(",", entries);
    }

    public void Handle_SC_ReturnToTown(SC_ReturnToTown p)
    {
        if (P2PDebugConfig.TraceCombat)
            Debug.Log("[ClientHandlers] Received ReturnToTown");
        ClientFlow.Instance.ReturnToTown();
    }

    public void Handle_SC_Inventory(SC_Inventory p)
    {
        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[ClientHandlers] Received Inventory: {p.itemss.Count} items, {p.equipmentss.Count} equips");

        if (InventoryManager.Instance == null)
        {
            var go = new GameObject("InventoryManager");
            go.AddComponent<InventoryManager>();

            if (ItemDataManager.Instance == null)
            {
                var dataGo = new GameObject("ItemDataManager");
                dataGo.AddComponent<ItemDataManager>();
            }
        }
        else
        {
            if (ItemDataManager.Instance == null)
            {
                var dataGo = new GameObject("ItemDataManager");
                dataGo.AddComponent<ItemDataManager>();
            }
        }

        InventoryManager.Instance.OnInventoryReceived(p);
    }

    public void Handle_SC_EquipResult(SC_EquipResult p)
    {
        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[ClientHandlers] EquipResult: Success={p.Success}, Item={p.InstanceId}, Equipped={p.Equipped}");

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnEquipResult(p);
    }

    public void Handle_SC_UpdateSkillSlots(SC_UpdateSkillSlots p)
    {
        if (P2PDebugConfig.TraceCombat)
        {
            string tmpItemName = "[";
            foreach (var item in p.activeSkillSlotss) tmpItemName += (item.SkillId + " | ");
            Debug.Log($"[ClientHandlers] SC_UpdateSkillSlots: NormalAttack={p.NormalAttackSkillId}, Skills={p.activeSkillSlotss.Count} | {tmpItemName} ]");
        }

        var skillIds = new List<string>();
        for (int i = 0; i < p.activeSkillSlotss.Count; i++)
            skillIds.Add(p.activeSkillSlotss[i].SkillId ?? "");

        if (RhythmInputController.Instance != null)
        {
            RhythmInputController.Instance.SetNormalAttackSkill(p.NormalAttackSkillId);
            for (int i = 0; i < 4; i++)
            {
                string skillId = i < skillIds.Count ? skillIds[i] : "";
                RhythmInputController.Instance.SetSkillSlot(i, skillId);
            }
        }

        var hud = HudPresenter.Instance != null
            ? HudPresenter.Instance
            : FindFirstObjectByType<HudPresenter>();
        if (hud != null)
            hud.ApplyServerSkillSlots(p.NormalAttackSkillId, skillIds);
    }
}
