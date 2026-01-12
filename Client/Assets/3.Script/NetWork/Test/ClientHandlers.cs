using com.yah.LineRendererDemo;
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    public void HandleSC_InitMap( SC_InitMap p)
    {
        var mapName = p.MapId; // <-- SC_InitGame에 string MapName 추가 필요

        var reg = MapRegistry.Instance;
        if (reg == null)
        {
            Debug.LogError("[InitGame] MapRegistry.Instance is null. Scene에 MapRegistry를 배치해야 함.");
            return;
        }

        if (!reg.TryGet(mapName, out var mapAsset) || mapAsset == null)
        {
            Debug.LogError($"[InitGame] MapAsset not found. mapName={mapName}. " +
                           $"MapAsset 이름과 서버 MapName을 통일했는지 확인.");
            return;
        }

        bool ok = GS.CreateMapFromAsset(mapAsset);
        if (!ok)
        {
            Debug.LogError($"[InitGame] CreateMapFromAsset failed. mapName={mapName}");
            return;
        }
    }

    public void Handle_SC_InitGame( SC_InitGame p )
    {
        Debug.Log("[In : Handle_SC_InitGame]");
        // 1) 맵 생성
        var mapName = p.MapName; // <-- SC_InitGame에 string MapName 추가 필요

        var reg = MapRegistry.Instance;
        if (reg == null)
        {
            Debug.LogError("[InitGame] MapRegistry.Instance is null. Scene에 MapRegistry를 배치해야 함.");
            return;
        }

        if (!reg.TryGet(mapName, out var mapAsset) || mapAsset == null)
        {
            Debug.LogError($"[InitGame] MapAsset not found. mapName={mapName}. " +
                           $"MapAsset 이름과 서버 MapName을 통일했는지 확인.");
            return;
        }

        RhythmClient.Instance.judgeWindowMs = (float)p.ActionWindowMs;
        Debug.Log($" Get JudgeWindowMS :{RhythmClient.Instance.judgeWindowMs}");

        bool ok = GS.CreateMapFromAsset(mapAsset);
        if (!ok)
        {
            Debug.LogError($"[InitGame] CreateMapFromAsset failed. mapName={mapName}");
            return;
        }
 

        // 2) 플레이어 Actor 정보
        var actorIds = p.playerActorIdss.Select(pa => pa.ActorId).ToArray();
        GS.SetPlayerActorIds(actorIds);
        GS.SetMyActorId(p.MyActorId);
        Debug.Log($"ActorId : {GS.MyActorId} ~!~!~@~!@~@");
        // 3) 엔티티 스폰
        GS.ClearEntities();
        foreach (var e in p.spawnEntitiess)
        {
            Debug.Log($"Spawn Entite [ ID :{e.EntityId} || Type : {(EntityType)e.EntityType} || (x,y) : ({e.X}, {e.Y}) || Hp : {e.Hp} ]");
            GS.SpawnOrUpdateEntity(new ClientEntityInfo
            {
                EntityId = e.EntityId,
                EntityType = e.EntityType,
                X = e.X,
                Y = e.Y,
                Hp = e.Hp
            });
        }

        // 4) 초기화 완료 후 연출/카메라 등 (원하면 쪽에서 구현)
        GS.OnInitGameCompleted();
    }

    public void Handle_SC_CalibResult(SC_CalibResult p)
    {
        BeatDebugUI_TMP.Instance?.RecordServerDiff(p.DiffMs, p.BeatIndex, RhythmClient.Instance.GetCurrentServerTimeMs());
        AudioOffsetAutoCalibrator.Instance?.OnServerDiff(p.DiffMs);

    }



    /// <summary>
    /// 서버 기준 Beat/리듬 동기화
    /// </summary>
    public void Handle_SC_BeatSync(SC_BeatSync p)
    {
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
                }
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
            // 지금은 "Shape==Cells"만 확실히 처리 (서버가 Cells를 채워주는 구조라면 여기만으로 충분)
            if (t.Shape != 0 || t.cellss == null || t.cellss.Count == 0)
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


}




#region 이전 찌꺼기 
/*// 필요한 SC_* 핸들러들만 예시 (나머지는 기존 프로젝트에 맞게 추가)
*//*  public void Handle_SC_GameStartWithRollDice(SC_GameStartWithRollDice p)
  {
      GS.FirstDiceNonce = p.firstDiceRequestNonce;
      HUD.SetStatus("ROLL FIRST DICE REQUEST");
      HUD.EnableFirstDice(true);
  }
  public void Handle_SC_RollDiceForTurn(SC_RollDiceForTurn p)
  {
      GS.Turn = p.turn;
      GS.PlayerTurnSlot = p.playerTurnSlot;
      GS.DiceRequestNonce = p.diceRequestNonce;
      HUD.SetTurn(GS.Turn, GS.PlayerTurnSlot);
      HUD.EnableMove(GS.PlayerTurnSlot == GS.MySide);
      HUD.SetStatus(GS.PlayerTurnSlot == GS.MySide ? "당신의 턴: 이동 주사위를 굴리세요" : "상대의 턴");
  }
  public void Handle_SC_PieceMoveConfirm(SC_PieceMoveConfirm p)
  {
      HUD.EnableMove(false);
      if (p.paths != null && p.paths.Count > 0)
          BV.AnimatePath(GS.PlayerTurnSlot, p.pieceType, p.paths);
      else
          BV.TeleportToLast(GS.PlayerTurnSlot, p.pieceType);
      HUD.SetStatus(p.willFight ? "전투 발생!" : "이동 확정, 전투 없음");
  }
  public void Handle_SC_FightConfirm(SC_FightConfirm p)
  {
      GS.BattleNonce = p.battleNonce;
      HUD.EnableBattleCards(p.isFight);
      if (!p.isFight) { HUD.SetStatus("전투 없음"); return; }

      HUD.SetStatus($"전투 시작 (turn={p.turn}) - 카드 선택 (1~10)");
      if (p.piceInfos != null && p.piceInfos.Count > 0)
      {
          var fighters = p.piceInfos
              .Select(x => new PieceRef(
                  (PlayerSlot)x.playerSlotNum,
                  (PieceType)x.piceNum))
              .ToList();
          BV.HighlightFighters(fighters);
      }
  }
  public void Handle_SC_BattleResult(SC_BattleResult p)
  {
      HUD.EnableBattleCards(false);
      BV.TeleportTo(p.losePlayerSlotNum, p.losePiceNum, p.newX, p.newY);
      HUD.SetStatus($"전투 결과: {p.losePlayerSlotNum}/{p.losePiceNum} → ({p.newX},{p.newY})");
      if (p.isGameEnd) HUD.SetStatus("카드 소진으로 게임 종료 체크 중…");
  }
  // ClientHandlers.cs 내부에 추가*//*

public void Handle_SC_TurnEnd(SC_TurnEnd p)
{
    // 상태 표시 및 입력 잠금
    HUD.SetStatus($"턴 종료. 다음 턴: {p.nextPlayerTurnSlot}");
    HUD.EnableMove(false);
    HUD.EnableBattleCards(false);

    // (선택) 로컬 추정 업데이트 — 실제 턴 시작은 SC_RollDiceForTurn에서 확정됨
    try { GS.Turn = p.turn + 1; } catch { *//* PDL에 turn 없으면 무시 *//* }
}

public void Handle_SC_WhoIsWinner(SC_WhoIsWinner p)
{
    var w = p.winnerSlot;
    HUD.SetStatus(w < 0 ? "무승부" : $"승자: {w}");
    //  HUD.ShowGameOver(w);

    // 모든 입력 잠금
    HUD.EnableMove(false);
    HUD.EnableBattleCards(false);
    HUD.EnableFirstDice(false);
}*/
#endregion