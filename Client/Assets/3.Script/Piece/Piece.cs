using Cysharp.Threading.Tasks;
using Gameplay.TypeEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class Piece : MonoBehaviour
{
    [Header("움직임 속도제어")]
    [SerializeField] protected float moveSpeed = 20.0f; // 이동 속도
    [SerializeField] protected float turnSpeed = 40.0f; // 회전 속도
    [SerializeField] private Transform myHomePos;
    [SerializeField] public PieceType MyTpye { get; private set; }
    [SerializeField] protected Animator animator;
    protected IDirection myTeamDirection;
    public int CurrentX { get; set; }
    public int CurrentY { get; set; }
    protected List<Vector3> smoothedPath = new List<Vector3>(); // 곡선을 따라 이동할 경로

    public bool IsArrive { get; set; }
    private void Awake()
    {
        //집에 있을때: 99,99
        CurrentX = 99;
        CurrentY = 99;
        myTeamDirection = GetComponentInParent<IDirection>();
        if (myHomePos == null) Debug.Log("HomePos위치 없음");
        GoHome();
    }

    public void GoHome()
    {
        transform.position = myHomePos.position;
    }

    public void Attack()
    {
        //MasterAudio.PlaySound("attack");
        animator.SetTrigger("isAttack");
        //공격애니메이션
    }
    public void Hit()
    {
        animator.SetTrigger("isHit");
    }
    protected virtual bool IsInvalid(Direction d) => d == Direction.None || d == Direction.Diagonal;
    protected bool IsSame(Direction a, Direction b) => a != Direction.None && a == b;
    protected Direction Opposite(Direction d) => d switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => Direction.None
    };
    public virtual bool Move(List<Tuple<int, int>> routeIndex, int requiredMoveSpaces)
    {
        //마지막 이동이 None이면 처음
        Direction lastDirection = Direction.None;

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
            if (hasPrev && Opposite(lastDirection) == currentDirection) return false;          // 반대 방향 금지

            // 다음 턴을 위해 마지막 방향을 저장
            lastDirection = currentDirection;
        }

        CurrentX = routeIndex[routeIndex.Count - 1].Item1;
        CurrentY = routeIndex[routeIndex.Count - 1].Item2;
        return true;
    }

    async public void Move(Queue<Vector3> _route)
    {
        // 순간이동: _route의 첫 번째 점으로 이동
        if (_route.Count > 0)
        {
            Vector3 startPoint = _route.Peek(); // Queue의 첫 번째 점 가져오기
            transform.position = startPoint;   // 즉시 이동
        }

        // 원래 경로를 곡선으로 변환
        GenerateSmoothPath(_route);

        //경로를 따라 이동
        await MoveAlongSmoothPath();
    }

    private void GenerateSmoothPath(Queue<Vector3> _route)
    {
        List<Vector3> routePoints = new List<Vector3>(_route);

        // 첫 번째와 마지막 점에서의 곡선 각을 계산하기 위해 가상의 시작과 끝 점 추가
        routePoints.Insert(0, routePoints[0]);
        routePoints.Add(routePoints[routePoints.Count - 1]);

        smoothedPath.Clear();

        // Catmull-Rom 보간으로 각 구간의 곡선을 생성
        for (int i = 0; i < routePoints.Count - 3; i++)
        {
            Vector3 p0 = routePoints[i];
            Vector3 p1 = routePoints[i + 1];
            Vector3 p2 = routePoints[i + 2];
            Vector3 p3 = routePoints[i + 3];

            // 곡선을 따라 여러 점을 생성하여 부드러운 경로 구성
            for (float t = 0; t <= 1; t += 0.1f)
            {
                Vector3 position = CatmullRom(p0, p1, p2, p3, t);
                smoothedPath.Add(position);
            }
        }
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Catmull-Rom 보간 함수
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2 * p1) +
                       (-p0 + p2) * t +
                       (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                       (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }

    async public void TestMove()
    {
        transform.position = smoothedPath[0];
        await MoveAlongSmoothPath();
    }

    async protected virtual UniTask MoveAlongSmoothPath()
    {
        if (smoothedPath == null || smoothedPath.Count == 0) return;

        animator.SetBool("isMoving", true); // 이동 시작 애니메이션 설정

        transform.DOKill(true); // 겹치는 트윈/잔여 트윈 정리
        Vector3 from = transform.position;


        for (int i = 0; i < smoothedPath.Count; i++)
        {
            Vector3 to = smoothedPath[i];
            if ((to - from).sqrMagnitude < 0.0001f)
            {
                from = to;
                continue;
            }

            // 회전
            Vector3 dir = (to - from);
            dir.y = 0f; // 필요 시 제거, 수평면에서만 회전하게 함
            //sqrMagnitude를 쓰는 이유는 성능(제곱근 연산 회피). 부드럽게 회전하기위한 부분
            if (dir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized);
                float angle = Quaternion.Angle(transform.rotation, targetRot);
                float rotateTime = Mathf.Clamp(angle / Mathf.Max(1f, turnSpeed), 0.05f, 0.35f);

                await transform.DORotateQuaternion(targetRot, rotateTime)
                               .SetEase(Ease.Linear)
                               .AsyncWaitForCompletion();   // 회전 완료 대기
            }

            // 이동 시간 = 거리 / 속도
            float dist = Vector3.Distance(from, to);
            float moveTime = dist / Mathf.Max(0.0001f, moveSpeed);

            await transform.DOMove(to, moveTime)
                       .SetEase(Ease.Linear)
                       .AsyncWaitForCompletion();       // 이동 완료 대기

            from = to;
        }

        animator.SetBool("isMoving", false);
    }
}
