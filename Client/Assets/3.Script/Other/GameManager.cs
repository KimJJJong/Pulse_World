using Cysharp.Threading.Tasks;
using Gameplay.StateEnum;
using System;
using Gameplay.TypeEnum;
using UnityEngine;

[Serializable]
public struct PosTuple
{
    public int x;
    public int y;
}
[Serializable]
public class PosIndex
{
    public PosTuple[] index = new PosTuple[6];

    public void ClearIndex()
    {
        for (int i = 0; i < index.Length; i++)
        {
            index[i].x = 0;
            index[i].y = 0;
        }
    }
}

[Serializable]
public class OtherPlayerData
{
    [SerializeField] private TeamType myTeam;
    [SerializeField] private int diceValue;
    [SerializeField] private bool isMoved;
    [SerializeField] private bool isVictory;
    [SerializeField] private int selectedPiece;
    [SerializeField] private PosIndex route;
    private bool[] isArrivedPiecesCheckd;
    public TeamType GetMyTeam { get { return myTeam; } }
    public int GetDiceValue { get { return diceValue; } }
    public bool IsMoved { get { return isMoved; } set { isMoved = value; } }
    public bool IsVictory { 
        get 
        {
            foreach (var piece in isArrivedPiecesCheckd)
            {
                if (!piece)
                    return false;
            }
            return true;
        } 
    }
    public int GetSelectedPiece { get { return selectedPiece; } }
    public PosTuple[] GetRoute { get { return route.index; } }
    
    public void CheckArrivedPiece(int index)
    {
        isArrivedPiecesCheckd[index] = true;
    }

    public void ClearTurnData()
    {
        diceValue = 0;
        selectedPiece = 9;
        isMoved = false;
        route.ClearIndex();
    }
}

public class GameManager : Singleton<GameManager>
{
    public EGameState state;

    private TeamType turn, otherTurn;//tmp Player
    private Player myPlayer, otherPlayer;
    public OtherPlayerData otherPlayerData;

    [Header("Player Red / Blue")]
    [SerializeField] private Player playerRed;
    [SerializeField] private Player playerBlue;

    [SerializeField] private InGameUIController uiController;
    private Piece tmpPiece;

    //public Animator[] animator;

    /*FightLogic fightLogic;*/

    bool _switch;

    int drawCardCount;
    private bool isActiveUIController;

    /*EndUISpriteManager endingManager;*/

    private void Start()
    {
        otherPlayerData.ClearTurnData();
       /*if (NetClient.Instance._myGame.isFirst == 1)
       {
           myPlayer = player1;
       }
       else if (NetClient.Instance._myGame.isFirst == 2)
       {
           myPlayer = player2;
       }
       else
       {
           Debug.Log("Err");
       }
       Debug.Log($"myPlayer is {myPlayer}");*/

        //네트워크에서 받아온 내 팀에따라 배치
       myPlayer = playerBlue; //test
        otherPlayer = playerRed;

        uiController.SetPiecesAddListener(myPlayer); //사용할 버튼 연결

        uiController.TestFristRollingButton().Forget();
        state = EGameState.FirstRollingDice;
        //p2p NetWork
        //PacketState packet = new PacketState();
        //packet.gameState = state;
        //NetClient._userToken.Send(packet);

        //fightLogic = new FightLogic();
        //drawCardCount = 0;

        //piece위치 동기화
        //PacketPiecePos packetPiece = new PacketPiecePos();
        //packetPiece = SetPiecesPosition(myPlayer);
    }

    
    //============> 게임 내 플레이를 위한 처리로직
    public void WhoFirst()
    {
        // Player A Dice head vs Player B Dice head
        if (myPlayer.GetDiceValue != 0 && otherPlayerData.GetDiceValue != 0)
        {
            //턴 정리
            turn = (myPlayer.GetDiceValue > otherPlayerData.GetDiceValue) ? myPlayer.GetMyTeam : otherPlayerData.GetMyTeam;
            otherTurn = (turn == myPlayer.GetMyTeam) ? playerBlue.GetMyTeam : myPlayer.GetMyTeam;

            //정리되면 데이터 정리
            myPlayer.ClearTurnData();
            otherPlayerData.ClearTurnData();

            //턴이동
            Debug.Log($"{state} -> {EGameState.PickPiceAndMove}");
            state = EGameState.Stay;

            //p2p NetWork
            /*PacketState packet = new PacketState();
            packet.gameState = state;
            NetClient.Instance._P2P_userToken.Send(packet);*/

        }
    }

    public void StayOtherTurn()
    {
        /*if (turn == myPlayer.GetMyTeam)
            state = EGameState.PickPiceAndMove;*/
        state = EGameState.PickPiceAndMove;
    }
    public void Turn()    // 턴에 따라 클라이언트의 움직임을 활성화, 비활성화
    {
        //CameraCont.FollowPiece(turn.selectedPiece);         //카메라찡
        if (turn == myPlayer.GetMyTeam && myPlayer.GetDiceValue == 0)
        {
            uiController.ActiveRollingDiceButton(); //내턴이면 UI 활성화
        }
        else if (turn == otherPlayerData.GetMyTeam && otherPlayerData.GetDiceValue != 0 && !isActiveUIController)
        {
            //주사위 값 공개
            uiController.OtherPlayerRolledDice(otherPlayerData.GetDiceValue);
            isActiveUIController = true;
        }

        //캐릭터 선택
        if (turn == myPlayer.GetMyTeam && myPlayer.GetDiceValue != 0)
        {
            uiController.ActiveCharacterSlot(true);
        }
        //상대 턴이면 움직임 보여주기
        else if (turn == otherPlayerData.GetMyTeam && otherPlayerData.IsMoved)
        {
            //이동하는거 불러오자
            otherPlayer.TestMovePiece(otherPlayerData);

        }

        //캐릭터 움직임끝
        if (turn == myPlayer.GetMyTeam && myPlayer.IsMoved)       //turn 의 isMoved 를 확인하자!!!
        {
            uiController.ActiveCharacterSlot(false); //이것도 자동으로 해제되게 하자

            //동작이 끝날대마다 해당 piece가 상대방 진영에 도착했는지 확인 + 승리 조건 확인
            var arrivePieceIndex = myPlayer.IsArrivedPiece();
            if (arrivePieceIndex > 0)
            {
                //도착한 캐릭터의 선택슬롯은 없애기
                uiController.RemovePieceChoiceButton(arrivePieceIndex);
            }

            if (myPlayer.IsVictory())
            {
                Debug.Log($"{state} -> {EGameState.Victory}");
                state = EGameState.Victory;

                /*//p2p NetWork
                PacketState packet1 = new PacketState();
                packet1.gameState = state;
                NetClient._userToken.Send(packet1);*/

                return;
            }
            else if (turn == otherPlayerData.GetMyTeam && otherPlayerData.IsMoved)
            {
                var index = otherPlayer.IsArrivedPiece();
                otherPlayerData.CheckArrivedPiece(index);

                if (otherPlayerData.IsVictory)
                {
                    Debug.Log($"{state} -> {EGameState.Victory}");
                    state = EGameState.Victory;

                    /*//p2p NetWork
                    PacketState packet1 = new PacketState();
                    packet1.gameState = state;
                    NetClient._userToken.Send(packet1);*/

                    return;
                }
            }
            Debug.Log($"{state} -> {EGameState.MoveFinish}");
            //state = EGameState.MoveFinish;
            state = EGameState.TurnToss; //테스트용

            //p2p NetWork
            /*PacketState packet = new PacketState();
            packet.gameState = state;
            NetClient._userToken.Send(packet);*/

        }

    }


    /*void FightCheck()
    {



        for (int i = 0; i < 4; i++)
        {
            Debug.Log($"x : {turn.pieces[i].x} / y : {turn.pieces[i].y}");
            Debug.Log($"x : {otherTurn.pieces[i].x} / y : {otherTurn.pieces[i].y}");
        }
        Debug.Log($"x : {turn.selectedPiece.x} / y : {turn.selectedPiece.y}");
        // Debug.Log($"x : {otherTurn.selectedPiece.x} / y : {otherTurn.selectedPiece.y}");





        for (int i = 0; i < 4; i++)     // turn 의 selectedPiece와 otherTurn의 나머지 4 Piece의 위치 비교
        {
            if (turn.selectedPiece.x == otherTurn.pieces[i].x && turn.selectedPiece.y == otherTurn.pieces[i].y)
            {

                tmpPiece = otherTurn.pieces[i];

                Debug.Log($"{state} -> {EGameState.CardDrawing}");
                state = EGameState.CardDrawing;

                //p2p NetWork
                PacketState packet = new PacketState();
                packet.gameState = state;
                NetClient._userToken.Send(packet);

                return;
            }

        }
        Debug.Log($"{state} -> {EGameState.TurnToss}");
        state = EGameState.TurnToss;





    }*/

    /*void Fight()
    {
        if (!_switch)
        {
            turn.ActiveFightView(); // 카메라 설정
            drawCardCount++;

            if (myPlayer == player1)
                animator[0].SetTrigger("isShowCards");
            else if (myPlayer == player2)
                animator[1].SetTrigger("isShowCards");

            _switch = true;
        }


        //두 플레이어 모두가 카드를 선택 하였다면  -> Fight logic에 따라 진행
        if (turn.choiceCard != 0 && otherTurn.choiceCard != 0)
        {
            fightLogic.CompareLv(turn, otherTurn, tmpPiece, Math.Abs(turn.choiceCard - otherTurn.choiceCard));
            _switch = false;
            Debug.Log($"{state} -> {EGameState.TurnToss}");

            state = EGameState.TurnToss;

            //p2p NetWork
            *//*            PacketState packet = new PacketState();
                        packet.gameState = state;
                        NetClient._userToken.Send(packet);*//*

        }
    }*/

    public void SwapTurn()     //알거라 믿습니다 :>
    {
        
        myPlayer.ClearTurnData();   //초기화
        otherPlayer.ClearTurnData();   //초기화
        isActiveUIController = false;

        //이전턴이 내턴이었으면 상대에게 넘기기
        turn = (turn == myPlayer.GetMyTeam) ? otherPlayer.GetMyTeam : myPlayer.GetMyTeam;
        otherTurn = (turn == myPlayer.GetMyTeam) ? otherPlayer.GetMyTeam : myPlayer.GetMyTeam;

        //piece위치 동기화
        /*PacketPiecePos packetPiece = new PacketPiecePos();
        packetPiece = SetPiecesPosition(myPlayer);
        NetClient._userToken.Send(packetPiece);*/


        Debug.Log($"{state} -> {EGameState.PickPiceAndMove}");

        state = EGameState.Stay;


        if (otherTurn == myPlayer.GetMyTeam)
        {
            //p2p NetWork
            /*PacketState packetState = new PacketState();
            packetState.gameState = EGameState.TurnToss;
            NetClient._userToken.Send(packetState);*/

        }
    }

    /*void Victory()
    {
        if (drawCardCount >= 10)
        {
            //카드 소진시 게임 종료
            int turnValue = turn.EvaluateGameOutcome();
            int otherValue = otherTurn.EvaluateGameOutcome();
            //점수가 큰사람이 진거
            if (turnValue > otherValue) { Debug.Log("Other Turn Wins!"); }
            else if (turnValue < otherValue) { Debug.Log("Turn Wins!"); }
            else { Debug.Log("It's a draw!"); }
        }

        endingManager.ShowEnding(1); //이김
        endingManager.ShowEnding(0); //짐

        print($"Winer : {turn.id}");
    }*/

    /*public Player GetOpponent()
    {
        return (myPlayer == player1) ? player2 : player1;
    }
    public Player GetOpponentTurn()
    {
        return (myPlayer == turn) ? otherTurn : turn;
    }

    public PacketPiecePos SetPiecesPosition(Player player)
    {
        PacketPiecePos piecePos = new PacketPiecePos();
        for (int i = 0; i < 4; i++)
        {
            piecePos.pos[i].x = player.pieces[i].x;
            piecePos.pos[i].y = player.pieces[i].y;
        }
        return piecePos;
    }*/
}
