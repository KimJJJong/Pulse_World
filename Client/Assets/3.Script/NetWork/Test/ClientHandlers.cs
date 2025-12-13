using ServerCore;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.HID;

public class ClientHandlers : MonoBehaviour
{
    public static ClientHandlers Instance { get; private set; }
    public ClientGameState GS => ClientGameState.Instance;

    public RhythmClient Rhythm => RhythmClient.Instance;

    //TestHUD HUD => TestHUD.Instance;
    //BoardView BV => BoardView.Instance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }


    public void Handle_SC_InitGame( SC_InitGame p )
    {

        // 1) 맵 생성
        GS.CreateMap(p.MapWidth, p.MapHeight);

        int idx = 0;
        foreach (var t in p.tiless)
        {
            int x = idx % p.MapWidth;
            int y = idx / p.MapWidth;
            GS.SetTile(x, y, t.TileKind);
            idx++;
        }

        // 2) 플레이어 Actor 정보
        var actorIds = p.playerActorIdss.Select(pa => pa.ActorId).ToArray();
        GS.SetPlayerActorIds(actorIds);
        GS.SetMyActorId(p.MyActorId);
        Debug.Log($"ActorId : {GS.MyActorId} ~!~!~@~!@~@~!@~!@");
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

    /// <summary>
    /// 서버 기준 Beat/리듬 동기화
    /// </summary>
    public void Handle_SC_BeatSync(SC_BeatSync p)
    {
        Rhythm.OnBeatSync(new BeatSyncInfo
        {
            ServerTimeMs = p.ServerTimeMs,
            SongStartServerTimeMs = p.SongStartServerTimeMs,
            Bpm = p.Bpm,
            BaseBeatDivision = p.BaseBeatDivision,
            BeatIndex = p.BeatIndex
        });
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

            GS.OnBeatAction(action);
        }
    }
    public void Handle_SC_Warn(SC_Warn p)
    {
        Debug.LogWarning($"[SC_Warn] code={p.code} msg={p.msg}");
        // TODO: HUD 팝업 등 필요하면 여기서 처리
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