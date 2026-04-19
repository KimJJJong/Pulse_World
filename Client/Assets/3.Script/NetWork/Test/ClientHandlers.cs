using ServerCore;
using System;
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

    private BoardView BV => BoardView.Instance;
    void Awake()
    {
        Instance = this;
    }


    public void HandleSC_InitMap(SC_InitMap p)
    {
        Debug.Log($"[ClientHandlers] HandleSC_InitMap: MapId={p.MapId} MyActorId={p.MyActorId}");

        // 1) 맵 생성
        var mapName = p.MapId;

        var reg = MapRegistry.Instance;
        if (reg == null)
        {
            Debug.LogError("[InitMap] MapRegistry.Instance is null. MapRegistry가 씬에 배치되어 있어야 합니다.");
            return;
        }

        if (!reg.TryGet(mapName, out var mapAsset) || mapAsset == null)
        {
            Debug.LogError($"[InitMap] MapAsset not found: {mapName}");
            return;
        }

        if (p.MapWidth > 0 && p.MapHeight > 0 &&
            (mapAsset.Width != p.MapWidth || mapAsset.Height != p.MapHeight))
        {
            Debug.LogWarning(
                $"[InitMap] Map size mismatch for {mapName}. Packet=({p.MapWidth}x{p.MapHeight}) Asset=({mapAsset.Width}x{mapAsset.Height})");
        }

        // [InitMap_Warn] Ping/Pong 워밍업이 완료되지 않은 채 OnBeatSync가 호출되면
        // TimeSync.OffsetMs가 아직 0 또는 초기값이라 ServerSongStartMs와 시간 축이 어긋난다 (Root Cause A).
        // 원격에서 "Warning이 처음 몇 초 동안만 깜빡이다 안정화"되는 현상의 원인이 이것인지 확인용.
        if (TimeSync.EstimatedRttMs <= 0)
        {
            Debug.LogWarning($"[InitMap_Warn] Ping/Pong warmup not ready! OffsetMs={TimeSync.OffsetMs:F0}, " +
                             $"EstimatedRttMs={TimeSync.EstimatedRttMs:F0}. SongStart sync may drift by RTT/2 until next Pong.");
        }

        // Rhythm 동기화 (Town에서도 BGM 싱크 등을 위해 사용 가능)
        if (RhythmClient.Instance != null)
        {
            RhythmClient.Instance.judgeWindowMs = (float)p.ActionWindowMs;
            RhythmClient.Instance.OnBeatSync(new BeatSyncInfo
            {
                SongStartServerTimeMs = p.SongStartServerTime,
                Bpm = p.Bpm,
                BaseBeatDivision = p.BaseBeatDivision,
            });
            OnBeatSyncReady?.Invoke();
            Debug.Log($"[InitMap] Rhythm Sync: Bpm={p.Bpm}, SongStart={p.SongStartServerTime}");
        }

        GS.StartMapGeneration(mapAsset);

        // 2) 플레이어 Actor 정보
        var actorIds = p.playerss.Select(pa => pa.ActorId).ToArray();
        GS.SetPlayerActorIds(actorIds);
        GS.SetMyActorId(p.MyActorId);
        Debug.Log($"[InitMap] MyActorId: {GS.MyActorId}, TotalEntities: {p.entitiess.Count}");

        // 3) 엔티티 스폰
        GS.ClearEntities();
        foreach (var e in p.entitiess)
        {
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
        Debug.Log("InHandelbeatSync");
        Rhythm.OnBeatSync(new BeatSyncInfo
        {
            SongStartServerTimeMs = p.SongStartServerTimeMs,
            Bpm = p.Bpm,
            BaseBeatDivision = p.BaseBeatDivision,
            BeatIndex = p.BeatIndex
        });

        OnBeatSyncReady?.Invoke();

        Debug.Log($"SongStart={Rhythm.ServerSongStartMs} ServerNow={Rhythm.GetCurrentServerTimeMs()} diff={Rhythm.ServerSongStartMs - Rhythm.GetCurrentServerTimeMs()}ms");
    }

    /// <summary>
    /// 즉각적인 공격/스킬 브로드캐스트 처리 (애니메이션 선행 재생)
    /// </summary>
    public void Handle_SC_ActionInstantBroadcast(SC_ActionInstantBroadcast p)
    {
        if (BV == null) return;

        // 내 캐릭터는 로컬 Prediction으로 이미 재생되므로 서버 브로드캐스트는 무시
        if (p.ActorId == GS.MyActorId) return;

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

            // 1) 이동/행동 반영
            GS.OnBeatAction(action);

            // 2) HP 업데이트 반영 및 데미지 타이밍 추적
            if (a.hpUpdates != null && a.hpUpdates.Count > 0)
            {
                long clientBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;
                long serverNow  = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentServerTimeMs() : 0;

                // [DamageRecv] HP 업데이트 수신 — 애니메이션이 시작된 시점 대비 얼마나 늦게 오는지 확인용.
                // packetBeat < clientBeat 이면 이미 '지나간' 비트의 판정이 뒤늦게 도착한 것 (RTT 지연).
                // 원격에서 beatGap이 2 이상이면 데미지가 애니메이션보다 1~2 비트 늦게 반영됨 → 애니/데미지 분리.
                long beatGap = clientBeat - p.BeatIndex;
                Debug.Log($"[DamageRecv] actor={a.ActorId} packetBeat={p.BeatIndex} clientBeat={clientBeat} " +
                          $"beatGap={beatGap} (positive=late) serverNow={serverNow} hpUpdates={a.hpUpdates.Count} " +
                          $"rtt={TimeSync.EstimatedRttMs:F0}ms");

                foreach (var u in a.hpUpdates)
                {
                    if (GS.TryGetEntity(u.EntityId, out var info))
                    {
                        int oldHp = info.Hp;
                        info.Hp = u.NewHp;
                        Debug.Log($"[DamageRecv] HP_Change entity={u.EntityId} {oldHp}→{u.NewHp} (delta={u.NewHp - oldHp})");

                        GS.UpdateEntityState(info);
                    }
                    else
                    {
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
                    long beatGap = clientBeat - p.BeatIndex;
                    Debug.Log($"[WarningRecv] caster={t.CasterId} cell=({cell.X},{cell.Y}) " +
                              $"packetBeat={p.BeatIndex} clientBeat={clientBeat} beatGap={beatGap} " +
                              $"durationBeats={durationBeats} expireBeat={expireBeat} rtt={TimeSync.EstimatedRttMs:F0}ms");
                }
            }
        }
    }


    public void Handle_SC_Warn(SC_Warn p)
    {
        Debug.LogWarning($"[SC_Warn] code={p.code} msg={p.msg}");
    }

    public void Handle_SC_EntityDespawn(SC_EntityDespawn p)
    {
        int id = p.EntityId;
        bool removed = GS.RemoveEntity(id);

        Debug.Log($"[SC_EntityDespawn] entityId={id} removed={removed}");

        if (id == GS.MyActorId)
        {
            Debug.LogWarning("[SC_EntityDespawn] My actor despawned. Disable input / show UI.");
        }
    }

    public void Handle_SC_EntitySpawnHandler(SC_EntitySpawn p)
    {
        int id = p.EntityId;
        bool exists = GS.TryGetEntity(id, out var ent);
        if (exists)
        {
            Debug.LogWarning($"이미 Entity가 존재합니다 ID{id}|| MyId : {GS.MyActorId}");
            return;
        }

        var entity = new ClientEntityInfo
        {
            EntityId = p.EntityId,
            EntityType = p.EntityType,
            AppearanceId = p.AppearanceId,
            X = p.X,
            Y = p.Y,
            Hp = p.Hp
        };

        GS.SpawnOrUpdateEntity(entity);

        Debug.Log($"[SC_EntitySpawn] entityId={entity.EntityId} spawn ");

        if (id == GS.MyActorId)
        {
            Debug.LogWarning("[SC_EntitySpawn] My actor overload. Disable / show UI.");
        }
    }

    public void Handle_SC_ReturnToTown(SC_ReturnToTown p)
    {
        Debug.Log("[ClientHandlers] Received ReturnToTown");
        ClientFlow.Instance.ReturnToTown();
    }

    public void Handle_SC_Inventory(SC_Inventory p)
    {
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
        Debug.Log($"[ClientHandlers] EquipResult: Success={p.Success}, Item={p.InstanceId}, Equipped={p.Equipped}");

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnEquipResult(p);
    }

    public void Handle_SC_UpdateSkillSlots(SC_UpdateSkillSlots p)
    {
        string tmpItemName = "[";
        foreach (var item in p.activeSkillSlotss) tmpItemName += (item.SkillId + " | ");

        Debug.Log($"[ClientHandlers] SC_UpdateSkillSlots: NormalAttack={p.NormalAttackSkillId}, Skills={p.activeSkillSlotss.Count} | {tmpItemName} ]");

        if (RhythmInputController.Instance != null)
        {
            RhythmInputController.Instance.SetNormalAttackSkill(p.NormalAttackSkillId);
            for (int i = 0; i < p.activeSkillSlotss.Count; i++)
            {
                RhythmInputController.Instance.SetSkillSlot(i, p.activeSkillSlotss[i].SkillId);
            }
        }
    }
}
