using Gameplay.StateEnum;
using UnityEngine;

public class GameStateMachine : MonoBehaviour
{
    GameManager gm;
    private void Start()
    {
        gm = GameManager.Instance;
    }


    private void Update()
    {

        switch (gm.state)
        {
            case EGameState.FirstRollingDice:
                gm.WhoFirst();
                break;
            case EGameState.Stay:
                gm.StayOtherTurn();
                break;
            case EGameState.PickPiceAndMove:
                gm.Turn();
                break;
            case EGameState.MoveFinish:
                //FightCheck();
                break;
            case EGameState.CardDrawing:
                //Fight();
                break;
            case EGameState.TurnToss:
                gm.SwapTurn();
                break;
            case EGameState.Victory:
                //Victory();
                break;
        }
    }
}
