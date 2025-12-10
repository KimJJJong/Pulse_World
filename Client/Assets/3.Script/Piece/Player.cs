using Cysharp.Threading.Tasks;
using Gameplay.TypeEnum;
using System.Collections.Generic;
using UnityEngine;


public class Player : MonoBehaviour
{
    /// <summary>
    /// King: 0
    /// Queen: 1
    /// Jack: 2
    /// Jocker: 3
    /// None:4
    /// </summary>
    private ITeamType myTeam;
    [SerializeField] private Piece[] pieces;
    [SerializeField] private Board board;

    //[HideInInspector] public List<Card> cards = new List<Card>();

    public bool IsMoved { get; private set; }
    public int ChoiceCard { get; private set; }
    public TeamType GetMyTeam { get; private set;}

    public Piece selectedPiece { get; private set; }
    private int tmpPieceIndex;

    //서버로 바뀌면 안씀
    [Header("TestDice")]
    [SerializeField]private RollDice dice;
    public int GetDiceValue { get { return diceValue; } }
    private int diceValue;


    //public CameraSwitcher myCams;

    private void Awake()
    {
        myTeam = GetComponent<ITeamType>();
        if(myTeam != null) GetMyTeam = myTeam.GetTeamType();
        ClearTurnData();
    }

    void OnEnable() => dice.OnRolled += HandleDice;
    void OnDisable() => dice.OnRolled -= HandleDice;

    void HandleDice(int value)
    {
        diceValue = value;
    }

    public void ClearTurnData()
    {
        IsMoved = false;
        ChoiceCard = 0;
        diceValue = 0;
    }

    //버튼에 연결됨
    public void ChoicePiece( int i = 0)
    {
        if (diceValue == 0) { Debug.Log("다이스 초기화 안됨"); return; } 
        //플레이어 기물 선택 UI랑 연결[임시]
        selectedPiece = pieces[i];
        print(selectedPiece.name + "선택!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!1");
        //스타트 위치
        if (selectedPiece.CurrentX == 99 && selectedPiece.CurrentY == 99) //집에서 시작한다면
        {
            (int col, int row) = myTeam.GetStartPos();
            selectedPiece.CurrentX = row;
            selectedPiece.CurrentY = col;
            selectedPiece.transform.position = board.GetPlatformPos(col,row);
        }

        if (IsMoved)
            MovePiece().Forget();
    }

    private async UniTask MovePiece()
    {
        bool is_true = false;
        (int x, int y) = (selectedPiece.CurrentX, selectedPiece.CurrentY);
        //플레이어 카메라 전환
        //myCams.SetGridCamPriority(x, y);
        while (!is_true)
        {
            await board.DrawRoute(diceValue + 1, selectedPiece.CurrentX, selectedPiece.CurrentY,
               ReturnIndexValue => { is_true = selectedPiece.Move(ReturnIndexValue, diceValue + 1); },
               ReturnPosValue =>
               {
                   if (is_true)
                   {
                       //==>서버연결
                       selectedPiece.Move(ReturnPosValue);
                       //PacketPath packetPath = new PacketPath();
                       //packetPath = ConvertQueueToPacketPath(ReturnPosValue);
                       //packetPath.selectedPiece = (int)selectedPiece.type;
                       //NetClient._userToken.Send(packetPath);
                   }
                   else { print("다시 그리자"); }
               });
        }
        print("이동 끝");

        //카메라 전환
        //myCams.SwitchToAllview();
        //myCams.ResetGridCamPriority(x, y);

        IsMoved = true;
        // is Move 확인
        //PacketPlayerState packet = new PacketPlayerState();
        //packet.isMoved = InGameManager.Instance.turn.isMoved;
        //NetClient._userToken.Send(packet);
    }

    public void TestMovePiece(OtherPlayerData otherData)
    {
        var index = otherData.GetSelectedPiece;
        selectedPiece = pieces[index];

        PosTuple[] route = otherData.GetRoute;
        Queue<Vector3> pos = new Queue<Vector3>();
        //넘겨줄 값으로 변환
        foreach(var data in route)
        {
            pos.Enqueue(board.GetPlatformPos(data.x, data.y));
        }

        selectedPiece.Move(pos);
        //카메라 무빙끝나면 값받아와서 true로 할 것
        IsMoved = true;
    }

   
    //UI 카드 선택 [임시]
    /*    public void DrawCard(Card card)
        {
            //두플레이어의 기물이 서로 만났다고 가정
            //플레이어가 카드냄
            if (cards != null && cards.Contains(card))
            {
                choiceCard = card.value;

                //NetWork Send choiceCard
                if (InGameManager.Instance.myPlayer == InGameManager.Instance.turn)
                {
                    PacketPlayerOtherTurnCard packet = new PacketPlayerOtherTurnCard();
                    packet.choiceCard = choiceCard;
                    NetClient._userToken.Send(packet);
                }
                else if (InGameManager.Instance.myPlayer == InGameManager.Instance.otherTurn)
                {
                    PacketPlayerTurnCard packet = new PacketPlayerTurnCard();
                    packet.choiceCard = choiceCard;
                    NetClient._userToken.Send(packet);
                }

                card.Draw();
                cards.Remove(card);//내가 가진 리스트에서 없애고
                //카드 내는 모션은 Card스크립트에서 Draw() 안에추가
            }
            else
            {
                print("카드 없음");
                //게임 종료함
                choiceCard = 99;
            }
            myCams.SwitchToAllview();
            print(choiceCard);
        }*/

    /*    public void BackPiece(int num, Piece piece)
        {
            if (id == Team.Red)
            {
                piece.x -= num;
                if (piece.x < 0)
                {
                    //집으로 보내고 코드 끝내기
                    piece.GoHomePiece();
                    isMoved = true;

                    return;
                }

            }
            else
            {
                piece.x += num;
                if (piece.x > 7)
                {
                    //집으로 보내고 코드 끝내기
                    piece.GoHomePiece();
                    isMoved = true;


                    return;
                }
            }


            piece.transform.position = board.GetPlatformPos(piece.x, piece.y); //자연스러운 이동 구현할려면 코드 또 필요행
            isMoved = true; //이거 꼭 확인 되면 초기화 해줘

        }*/

    //카드를 다쓰면 턴 종료 =>이거는 GM에 옮기자
    public int EvaluateGameOutcome()
    {
        int[] scoreByXIndex;
        int value = 0;
        (int x, int y) zeroScorePosition = myTeam.GetStartPos();

        //나중에 마저 수정하자

        if (myTeam.GetTeamType() == TeamType.Blue)
        {
            scoreByXIndex = new int[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        }
        else // Team.Red
        {
            scoreByXIndex = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        }

        foreach (var piece in pieces)
        {
            if (piece.CurrentX == 99 && piece.CurrentY == 99)
            {
                value += 10; // 집에 있는 경우
            }
            else if (piece.CurrentX == zeroScorePosition.x && piece.CurrentY == zeroScorePosition.y)
            {
                value += 0; // 특정 위치의 0점 처리
            }
            else if (piece.CurrentX >= 0 && piece.CurrentX < scoreByXIndex.Length)
            {
                value += scoreByXIndex[piece.CurrentX]; // 배열을 통한 점수 추가
            }
        }

        return value;
    }

    public bool IsVictory()
    {
        foreach (var piece in pieces)
        {
            if (!piece.IsArrive)
                return false;
        }
        return true;
    }


    public int IsArrivedPiece()
    {
        (int x, int y) posIndex = myTeam.GetArrivedPos();
        if(posIndex.x == selectedPiece.CurrentX && posIndex.y == selectedPiece.CurrentY)
        {
            selectedPiece.IsArrive = true;
            return (int)selectedPiece.MyTpye;
        }
        return -1;
    }

    public void ActiveFightView()
    {
        //옵저버 -> 카메라 구독하자
        //myCams.SwitchToFightView(selectedPiece.transform);
    }

}
