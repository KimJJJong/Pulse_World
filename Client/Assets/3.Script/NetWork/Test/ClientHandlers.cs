using ServerCore;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using System.Collections.Generic;

public class ClientHandlers : MonoBehaviour
{
    public static ClientHandlers Instance { get; private set; }
    public ClientGameState GS => ClientGameState.Instance;

    public RhythmClient Rhythm => RhythmClient.Instance;

    public static event System.Action OnBeatSyncReady;


    private readonly System.Collections.Generic.Dictionary<(int x, int y), long> _telegraphExpireBeat
        = new System.Collections.Generic.Dictionary<(int x, int y), long>();

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

        bool ok = GS.CreateMapFromAsset(mapAsset);
        if (!ok)
        {
            Debug.LogError($"[InitMap] CreateMapFromAsset failed. mapName={mapName}");
            return;
        }

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
                Accepted = a.Accepted
            };

            // 1) 이동/행동 반영
            GS.OnBeatAction(action);

            // 2)  HP 변경 반영 (피격자들)
            if (a.hpUpdates != null && a.hpUpdates.Count > 0)
            {
                Debug.Log($"[Handle_SC_BeatActions] hpUpdates count={a.hpUpdates.Count}");
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
            CleanupExpiredTelegraphs(p.BeatIndex);
        }
    }

    public void Handle_SC_BeatTelegraphs(SC_BeatTelegraphs p)
    {
        //Debug.Log($"[SC_BeatTelegraphs] beat={p.BeatIndex} count={p.telegraphss.Count}"); 

        // BoardView가 없으면(씬 미배치) 일단 로그만
        if (BV == null)
        {
            Debug.LogWarning("[SC_BeatTelegraphs] BoardView.Instance is null. telegraph render skip.");
            return;
        }

        for (int i = 0; i < p.telegraphss.Count; i++)
        {
            var t = p.telegraphss[i];



            // ===== 적용 범위 계산 =====
            // Shape가 0이 아니더라도 서버가 Cells를 계산해서 보내준다면 사용 가능하도록 수정
            // Log for debugging
            //Debug.Log($"[Telegraph] Shape={t.Shape} Duration={t.DurationBeats} Cells={t.cellss?.Count ?? 0}");

            if (t.cellss == null || t.cellss.Count == 0)
                continue;

            // 이 텔레그래프는 몇 beat까지 유지?
            // - 예: beat=10, duration=2면 10~11 표시, 12부터 원복시키고 싶다
            // - 그러면 expireBeat = 10 + duration
            long expireBeat = p.BeatIndex + t.DurationBeats;

            for (int c = 0; c < t.cellss.Count; c++)
            {
                var cell = t.cellss[c];
                int x = cell.X;
                int y = cell.Y;

                // 빨강 덮기
                BV.SetTelegraphOverlay(x, y, on: true);

                // 동일 셀에 텔레그래프가 겹칠 수 있으니, 더 "늦게 끝나는" expire를 유지
                var key = (x, y);
                if (_telegraphExpireBeat.TryGetValue(key, out var prevExpire))
                {
                    if (expireBeat > prevExpire)
                        _telegraphExpireBeat[key] = expireBeat;
                }
                else
                {
                    _telegraphExpireBeat[key] = expireBeat;
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
    private void CleanupExpiredTelegraphs(long currentBeat)
    {
        if (currentBeat == _lastTelegraphCleanupBeat)
            return;
        _lastTelegraphCleanupBeat = currentBeat;

        if (_telegraphExpireBeat.Count == 0)
            return;
        if (BV == null)
            return;

        var toRemove = new System.Collections.Generic.List<(int x, int y)>();

        foreach (var kv in _telegraphExpireBeat)
        {
            var cell = kv.Key;
            long expireBeat = kv.Value;

            if (expireBeat <= currentBeat)
            {
                BV.SetTelegraphOverlay(cell.x, cell.y, on: false);
                toRemove.Add(cell);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
            _telegraphExpireBeat.Remove(toRemove[i]);
    }


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
        bool spawn = GS.TryGetEntity(id, out var ent );
        if( spawn)
        {
            Debug.LogWarning($"이미 Entity가 존재합니다 ID{id}|| MyId : {GS.MyActorId}");
            return;
        }

        var entity = new ClientEntityInfo
        {
            EntityId = p.EntityId,
            EntityType = p.EntityType,
            X = p.X,
            Y = p.Y,
            Hp = p.Hp
        };

        // 1) GS에서 논리 제거 + WorldView(=BoardView)에게 despawn 알림까지 전파
        GS.UpdateEntityState(entity);


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
}



