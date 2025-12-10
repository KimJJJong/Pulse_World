using UnityEngine;
using Gameplay.TypeEnum;

public class Red : MonoBehaviour, IDirection, ITeamType
{
    readonly (int, int) restartPosindex = (0, 3);
    readonly (int, int) arrivedPosindex = (8, 3);
    readonly int[] scoreByXIndex = new int[] { 1, 2, 3, 3, 4, 5, 6, 7, 8 };
    readonly TeamType team = TeamType.Red;
    public (int, int) GetStartPos() { return restartPosindex; }
    public (int, int) GetArrivedPos() { return arrivedPosindex; }
    public TeamType GetTeamType() { return team; }
    public Direction GetDirection(int startRow, int startCol, int endRow, int endCol)
    {
        // Red + 기물 기준으로 측정
        // 0.3 시작점

        int dr = endRow - startRow;
        int dc = endCol - startCol;

        return (dr, dc) switch
        {
            (1, 0) => Direction.Up,
            (-1, 0) => Direction.Down,
            (0, 1) => Direction.Left,
            (0, -1) => Direction.Right,
            (1, 1) or (1, -1) or (-1, 1) or (-1, -1) => Direction.Diagonal,
            _ => Direction.None // 인접이 아니거나 동일칸 등
        };

    }
}
