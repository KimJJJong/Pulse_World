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
    // private readonly System.Collections.Generic.Dictionary<(int x, int y), long> _telegraphExpireBeat ...
    
    private long _lastTelegraphCleanupBeat = long.MinValue;

    private BoardView BV => BoardView.Instance;
    void Awake()
    {
        //if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
   

    public void HandleSC_InitMap(SC_InitMap p )
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
            // 실패 시에도 계속 진행할지 여부? 일단 Return
            return;
        }

        if (p.MapWidth > 0 && p.MapHeight > 0 &&
            (mapAsset.Width != p.MapWidth || mapAsset.Height != p.MapHeight))
        {
            Debug.LogWarning(
                $"[InitMap] Map size mismatch for {mapName}. Packet=({p.MapWidth}x{p.MapHeight}) Asset=({mapAsset.Width}x{mapAsset.Height})");
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

        // CreateMapFromAsset -> Co_CreateMapFromAsset (Coroutine)
        // ClientHandlers는 MonoBehaviour이므로 StartCoroutine 가능
        GS.StartMapGeneration(mapAsset);
        
        // 주의: 맵 생성이 비동기로 돌기 때문에, 아래 로직들이 맵 생성 완료 전에 실행될 수 있음.
        // 하지만 Actor/Entity 세팅은 맵 타일과 독립적인 경우가 많음.
        // 만약 맵 타일에 의존적인 로직(예: 스폰 위치 유효성 체크 등)이 있다면
        // GS.IsMapGenerationComplete를 기다리는 코루틴으로 감싸야 함.
        // 현재 구조상으로는 Entity Spawn이 좌표만 저장하므로 괜찮을 것으로 판단됨.

        // 2) 플레이어 Actor 정보
        var actorIds = p.playerss.Select(pa => pa.ActorId).ToArray();
        GS.SetPlayerActorIds(actorIds);
        GS.SetMyActorId(p.MyActorId);
        Debug.Log($"[InitMap] MyActorId: {GS.MyActorId}, TotalEntities: {p.entitiess.Count}");

        // 3) 엔티티 스폰 (기존 엔티티 클리어 후 생성)
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

        // 4) 초기화 완료 알림 (로딩 화면 끄기 등)
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

            // 1) 이동/행동 반영
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
            //ServerTimeMs = p.ServerSendTimeMs,
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

        // 클라이언트 사이드 예측: 내 캐릭터는 서버 브로드캐스트를 기다리지 않고 로컬에서 즉시 실행하므로 서버 패킷은 무시합니다.
        if (p.ActorId == GS.MyActorId) return;

        // [NewSkill System] 데이터 기반 스킬 실행
        if (!string.IsNullOrEmpty(p.SkillId))
        {
            BV.PlaySkillInstant(p.ActorId, p.SkillId, p.Rotation, p.StartTick);
        }
        else
        {
            // 폴백: 레거시 애니메이션 시스템 (SkillId가 없는 경우만)
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
        //BV.CancelSkill(p.ActorId);
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

            if (a.hpUpdates != null && a.hpUpdates.Count > 0)
            {
                long clientBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;
                // [Action_Sync] 패킷 비트와 클라이언트 현재 비트를 출력하여 오차 확인
                Debug.Log($"[Action_Sync] PacketBeat:{p.BeatIndex} ClientBeat:{clientBeat} HPUpdates={a.hpUpdates.Count}");

                foreach (var u in a.hpUpdates)
                {
                    // u.EntityId, u.NewHp 가 있다고 가정
                    if (GS.TryGetEntity(u.EntityId, out var info))
                    {
                        info.Hp = u.NewHp;
                        Debug.Log($"[HP_Change] acterId : {u.EntityId} || hp : {u.NewHp}");

                        // 상태 갱신 + HUD 이벤트까지(Spawn 연출이 섞여있으면 별도 UpdateEntityState 추천)
                        GS.UpdateEntityState(info);
                    }
                    else
                    {
                        Debug.LogWarning($"[HP_Change] Entity not found: {u.EntityId}");
                    }
                }
            }
            else
            {
                 // Debug.Log($"[Handle_SC_BeatActions] No hpUpdates for actor {a.ActorId}");
            }

            // 기존 로직
            // [REMOVED] Cleanup is now handled by BoardView's Update loop
            // CleanupExpiredTelegraphs(p.BeatIndex);
        }
    }

    public void Handle_SC_BeatTelegraphs(SC_BeatTelegraphs p)
    {
        // [NewSkill System] 체크 
        // 해당 액터가 현재 데이터 기반 스킬(ClientSkillRunner)을 실행 중이라면 
        // 텔레그래프 렌더링을 중복으로 하지 않음 (깜빡임 방지)
        if (BV == null)
        {
            Debug.LogWarning($"[SC_BeatTelegraphs] BoardView.Instance is null. telegraph render skip. Beat={p.BeatIndex}");
            return;
        }

        long clientBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : -1;
        // Debug.Log($"[SC_BeatTelegraphs] Received: PacketBeat={p.BeatIndex}, CurrentBeat={clientBeat}, Count={p.telegraphss.Count}");

        for (int i = 0; i < p.telegraphss.Count; i++)
        {
            var t = p.telegraphss[i];

            // [핵심] 해당 액터가 새 시스템을 사용 중이더라도 레거시 패킷을 허용하여 
            // 지연 상황에서 안전 장치(Fallback)로 작동하게 합니다. (SetTelegraphOverlay는 Idempotent함)
            /*
            if (BV.IsActorRunningNewSkill(t.CasterId))
                continue;
            */



            // ===== 적용 범위 계산 =====
            // Shape가 0이 아니더라도 서버가 Cells를 계산해서 보내준다면 사용 가능하도록 수정
            // Log for debugging
            //Debug.Log($"[Telegraph] Shape={t.Shape} Duration={t.DurationTicks} Cells={t.cellss?.Count ?? 0}");

            if (t.cellss == null || t.cellss.Count == 0)
                continue;

            // 이 텔레그래프는 몇 beat까지 유지?
            // (DurationTicks + 479) / 480: 정수 나눗셈에서 올림 처리를 하여 최소 1비트 수명을 보장합니다.
            long expireBeat = p.BeatIndex + (t.DurationTicks + 479) / 480;

            for (int c = 0; c < t.cellss.Count; c++)
            {
                var cell = t.cellss[c];
                // [Change] 중앙 집중식 관리 시스템 사용 (만료 시간 전달)
                BV.SetTelegraphWithExpire(cell.X, cell.Y, expireBeat);
                
                // [Telegraph_Sync] 패킷 비트, 만료 비트, 클라이언트 현재 비트를 출력하여 오차 확인
                if (c == 0) // 오버헤드 방지를 위해 첫 번째 셀만 로깅
                {
                    Debug.Log($"[Telegraph_Sync] Caster:{t.CasterId} Cell:({cell.X},{cell.Y}) PacketBeat:{p.BeatIndex} ExpireBeat:{expireBeat} ClientBeat:{clientBeat}");
                }
            }
        }
    }


    public void Handle_SC_Warn(SC_Warn p)
    {
        Debug.LogWarning($"[SC_Warn] code={p.code} msg={p.msg}");
        // TODO: HUD 팝업 등 필요하면 여기서 처리
    }
    // [추가] 현재 beat 기준으로 만료된 셀들을 원복한다.
    /* [REMOVED] Centralized in BoardView
    private void CleanupExpiredTelegraphs(long currentBeat) ...
    */


    public void Handle_SC_EntityDespawn(SC_EntityDespawn p)
    {
        int id = p.EntityId;

        // 1) GS에서 논리 제거 + WorldView(=BoardView)에게 despawn 알림까지 전파
        bool removed = GS.RemoveEntity(id);

        Debug.Log($"[SC_EntityDespawn] entityId={id} removed={removed}");

        // 2) 내가 조종하던 actor가 사라졌으면(사망/퇴장) UI/입력 처리
        if (id == GS.MyActorId)
        {
            Debug.LogWarning("[SC_EntityDespawn] My actor despawned. Disable input / show UI.");
            // TODO: 입력 잠금/사망 UI 등
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
            AppearanceId = p.AppearanceId, // fix: ModelId -> AppearanceId
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
             // Lazy init if missing
             var go = new GameObject("InventoryManager");
             go.AddComponent<InventoryManager>(); // Awake will set Instance

             // Ensure Item Data is loaded
             if (ItemDataManager.Instance == null)
             {
                 var dataGo = new GameObject("ItemDataManager");
                 dataGo.AddComponent<ItemDataManager>();
             }
        }
        else
        {
            // Ensure UI exists on the existing manager

             // Ensure Item Data is loaded
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



