using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay.TypeEnum;

public class Jack : Piece
{
    //한번 되돌아갈 수 있음
    public override bool Move(List<Tuple<int, int>> routeIndex, int requiredMoveSpaces)
    {
        //마지막 이동이 None이면 처음
        Direction lastDirection = Direction.None;

        // 되돌아갔는지 여부를 추적하는 변수
        bool hasTurnedBack = false; 

        //좌표 중복 체크-최대 6개 리스트만쓰니까 순회 괜찮음
        bool hasDuplicates = routeIndex.Count != routeIndex.Distinct().Count();
        if (hasDuplicates) return false; //값에 중복이 있음

        if (routeIndex.Count != requiredMoveSpaces) return false;

        //기본 이동 체크
        for (int i = 1; i < routeIndex.Count; i++)
        {
            (int x1, int y1) = routeIndex[i - 1];
            (int x2, int y2) = routeIndex[i];

            if (!(x1 == CurrentX && y1 == CurrentY))
            {
                Debug.Log($"처음 위치가 잘못됨{x1}, {y1},{CurrentX},{CurrentY}");
                return false;
            }

            Direction currentDirection = myTeamDirection.GetDirection(x1, y1, x2, y2);

            bool hasPrev = lastDirection != Direction.None;

            if (IsInvalid(currentDirection)) return false;                 // 불가 방향
            if (hasPrev && IsSame(lastDirection, currentDirection)) return false;              // 같은 방향 두 번 금지
            if (hasPrev && Opposite(lastDirection) == currentDirection)
            {
                if (hasTurnedBack) return false;
                //한번 되돌아가기 가능
                hasTurnedBack = true;
            }

            // 다음 턴을 위해 마지막 방향을 저장
            lastDirection = currentDirection;
        }

        CurrentX = routeIndex[routeIndex.Count - 1].Item1;
        CurrentY = routeIndex[routeIndex.Count - 1].Item2;
        return true;
    }
}
