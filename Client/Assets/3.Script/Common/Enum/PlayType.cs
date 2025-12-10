namespace Gameplay.TypeEnum
{
    public enum TeamType { Red, Blue, None };
    public enum PieceType { King, Queen, Jack, Jocker };
    public enum Direction { Up, Down, Left, Right, Diagonal, None };
}

namespace Gameplay.StateEnum
{
    public enum EGameState
    {
        FirstRollingDice,
        PickPiceAndMove,
        MoveFinish,
        FightCheck,
        CardDrawing,
        TurnToss,
        Stay,
        Victory,
        None,
    }
}