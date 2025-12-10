using Gameplay.StateEnum;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum Side : sbyte { Me = 0, Opp = 1 }
[Serializable]
public struct PieceLite
{
    public int id;        // 0..N-1
    public Side side;     // Me / Opp
    public bool alive;    // 생존 여부
    public int sq;        // 위치(스퀘어 인덱스 = r*cols + c)
}

[Serializable]
public struct GameState
{
    // 보드
    public int rows, cols;

    // 점유 비트보드 (63칸이면 ulong 한 장에 충분)
    public ulong occMe;
    public ulong occOpp;

    // 말들
    public PieceLite[] pieces;

    // 손패(1~10) 비트마스크 (카드 쓰면 소모)
    public ushort handMe;
    public ushort handOpp;

    // 턴/남은 이동
    public Side current;
    public byte movesLeft;         // 남은 말 이동 횟수
    public byte maxMovesPerTurn;   // 보통 4

    // 편의
    public int Square(int r, int c) => r * cols + c;
    public (int r, int c) FromSquare(int sq) => (sq / cols, sq % cols);

    public bool IsTerminal() => false; // 여기서는 Apply만 다루니 비워둠(나중에 구현)
}

public readonly struct Undo
{
    public readonly int pieceId;
    public readonly int prevSq;

    public readonly bool captured;
    public readonly int capturedId;
    public readonly PieceLite prevCaptured; // 되살릴 때 쓸 원본

    public readonly ulong prevOccMe, prevOccOpp;
    public readonly ushort prevHandMe, prevHandOpp;
    public readonly Side prevSide;
    public readonly byte prevMovesLeft;

    public Undo(int pieceId, int prevSq, bool captured, int capturedId, PieceLite prevCaptured,
                ulong prevOccMe, ulong prevOccOpp, ushort prevHandMe, ushort prevHandOpp,
                Side prevSide, byte prevMovesLeft)
    {
        this.pieceId = pieceId;
        this.prevSq = prevSq;
        this.captured = captured;
        this.capturedId = capturedId;
        this.prevCaptured = prevCaptured;
        this.prevOccMe = prevOccMe;
        this.prevOccOpp = prevOccOpp;
        this.prevHandMe = prevHandMe;
        this.prevHandOpp = prevHandOpp;
        this.prevSide = prevSide;
        this.prevMovesLeft = prevMovesLeft;
    }
}

public readonly struct MovingBranch
{
    public readonly int pieceId;        // 어떤 기물이 움직이는지(배열/ID 인덱스)
    public readonly (int r, int c)[] route;
    public readonly bool isCombat;      // 도착 칸에 적이 있어서 전투가 발생하는가
    public readonly int? cardPlayed;    // 같은 랭크 전투일 때 내가 낼 카드(1~10). 아직 미정이면 null

    //이동 후보
    public MovingBranch(int pid, (int r, int c)[] r, bool combat, int? card = null)
    {
        pieceId = pid; route = r; isCombat = combat; cardPlayed = card;
    }
    public (int r, int c) From => route.Length > 0 ? route[0] : default;
    public (int r, int c) To => route.Length > 0 ? route[^1] : default;
}
public readonly struct ActionSequence
{
    public readonly MovingBranch[] moves;
    public int Length => moves?.Length ?? 0;
    public ActionSequence(MovingBranch[] m) { moves = m; }
    public MovingBranch this[int i] => moves[i];
}
public class MinimaxPlanner : MonoBehaviour
{
    readonly int minValue = 0; //모든말이 상대에게 잡혔을때 점수
    readonly int maxValue = 36; //상대 진영에 모두 들어갔을 때 점수
    readonly double searchTimeMs = 5000; //5초?
    readonly int maxDepth = 2; //탐색 깊이
    // 1) 반복 심화(Iterative Deepening) + 시간 예산
    public ActionSequence Plan(in GameState root, int diceValue)
    {
        var endAt = Time.realtimeSinceStartupAsDouble + searchTimeMs / 1000.0; //탐색 마감시각 계산.
        ActionSequence best = default;
        int bestScore = minValue;

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            int alpha = minValue + 1, beta = maxValue;
            var (score, firstMove) = SearchRoot(root, depth, alpha, beta, endAt);
            if (Time.realtimeSinceStartupAsDouble > endAt) break; // 시간 초과 시 마지막 완주 깊이 사용

            if (score > bestScore)
            {
                bestScore = score;
                best = new ActionSequence(new[] { firstMove }); // 루트 한 수만 채택(원하면 시퀀스로 확장)
            }
        }
        return best;
    }

    // 2) 루트에서 최선 수 고르기(α-β 적용)
    (int score, MovingBranch bestMove) SearchRoot(in GameState s, int depth, int alpha, int beta, double endAt)
    {
        MovingBranch best = default;
        int bestScore = int.MinValue;

        _bufMoves.Clear(); _bufExpanded.Clear();
        GenerateMovesWithCards(s, _bufMoves, _bufExpanded);
        OrderMovesHeuristic(s, _bufExpanded); // 캡처/전진 등 휴리스틱

        //각 수 적용 → 재귀 탐색 → 롤백.
        foreach (var m in _bufExpanded)
        {
            if (Time.realtimeSinceStartupAsDouble > endAt) break;

            var undo = Apply(ref Unsafe.AsRef(in s), m, out var next);
            int sc = -Search(next, depth - 1, -beta, -alpha, endAt); // 상대 차례 → negamax 형태
            Unapply(ref Unsafe.AsRef(in s), undo);

            if (sc > bestScore) { bestScore = sc; best = m; }
            if (sc > alpha) alpha = sc;        // α 갱신
            if (alpha >= beta) break;          // β 컷
        }
        return (bestScore, best);
    }

    // 3) 재귀 α-β(negamax 형태)
    int Search(in GameState s, int depth, int alpha, int beta, double endAt)
    {
        if (depth == 0 || s.IsTerminal() || Time.realtimeSinceStartupAsDouble > endAt)
            return Evaluate(s);

        _bufMoves.Clear(); _bufExpanded.Clear();
        GenerateMovesWithCards(s, _bufMoves, _bufExpanded);
        if (_bufExpanded.Count == 0) return Evaluate(s);

        OrderMovesHeuristic(s, _bufExpanded);

        int best = int.MinValue;
        foreach (var m in _bufExpanded)
        {
            var undo = Apply(ref Unsafe.AsRef(in s), m, out var next);
            int sc = -Search(next, depth - 1, -beta, -alpha, endAt);
            Unapply(ref Unsafe.AsRef(in s), undo);

            if (sc > best) best = sc;
            if (sc > alpha) alpha = sc;   // α 갱신
            if (alpha >= beta) break;     // β 컷
        }
        return best;
    }

    // ── 아래는 기존 코드에 있는 보조 멤버들(여기서는 선언만 가정) ──
    readonly List<MovingBranch> _bufMoves = new(64);
    readonly List<MovingBranch> _bufExpanded = new(64);

    void GenerateMovesWithCards(in GameState s, List<MovingBranch> buf, List<MovingBranch> outMoves) { /* 기존 구현 */ }
    void OrderMovesHeuristic(in GameState s, List<MovingBranch> moves) { /* 기존 구현 */ }
    int FindPieceIdAt(in GameState s, int sq)
    {
        var pieces = s.pieces;
        for (int i = 0; i < pieces.Length; i++)
            if (pieces[i].alive && pieces[i].sq == sq)
                return i;
        return -1;
    }

    Undo Apply(ref GameState s, in MovingBranch m, out GameState next)
    {
        // 1) 목적지 스퀘어 계산
        var toRC = m.To; // (r,c)
        int toSq = s.Square(toRC.r, toRC.c);

        // 2) 공격자 레퍼런스
        ref var attacker = ref s.pieces[m.pieceId];
        int fromSq = attacker.sq;

        // 3) Undo 스냅샷
        var undo = new Undo(
            pieceId: m.pieceId,
            prevSq: fromSq,
            captured: false,
            capturedId: -1,
            prevCaptured: default,
            prevOccMe: s.occMe,
            prevOccOpp: s.occOpp,
            prevHandMe: s.handMe,
            prevHandOpp: s.handOpp,
            prevSide: s.current,
            prevMovesLeft: s.movesLeft
        );

        // 4) 출발칸 점유 비트 내리기
        if (attacker.side == Side.Me) s.occMe &= ~(1UL << fromSq);
        else s.occOpp &= ~(1UL << fromSq);

        // 5) 전투 처리 (간단: 도착칸에 적이 있으면 제거)
        if (m.isCombat)
        {
            int defId = FindPieceIdAt(s, toSq);
            if (defId >= 0 && s.pieces[defId].side != attacker.side)
            {
                // 카드 사용(있다면) 소모
                if (m.cardPlayed.HasValue)
                {
                    int v = m.cardPlayed.Value; // 1..10
                    if (s.current == Side.Me) s.handMe = (ushort)(s.handMe & ~(1u << v));
                    else s.handOpp = (ushort)(s.handOpp & ~(1u << v));
                }

                // 캡처 기록(Undo용)
                var prevDef = s.pieces[defId];
                undo = new Undo(
                    pieceId: undo.pieceId,
                    prevSq: undo.prevSq,
                    captured: true,
                    capturedId: defId,
                    prevCaptured: prevDef,
                    prevOccMe: undo.prevOccMe,
                    prevOccOpp: undo.prevOccOpp,
                    prevHandMe: undo.prevHandMe,
                    prevHandOpp: undo.prevHandOpp,
                    prevSide: undo.prevSide,
                    prevMovesLeft: undo.prevMovesLeft
                );

                // 점유 비트에서 수비 제거 + 말 비활성
                if (prevDef.side == Side.Me) s.occMe &= ~(1UL << prevDef.sq);
                else s.occOpp &= ~(1UL << prevDef.sq);
                s.pieces[defId].alive = false;
            }
            // (주의) 실제 게임 규칙(계급/카드 비교)에 따라 승패 판정이 달라질 수 있음.
            // 지금은 "이동 후보가 이미 합법"이라고 가정하고 공격자 승으로 처리.
        }

        // 6) 공격자 이동 반영
        attacker.sq = toSq;
        if (attacker.side == Side.Me) s.occMe |= (1UL << toSq);
        else s.occOpp |= (1UL << toSq);

        // 7) 이동 횟수/턴 전환
        if (s.movesLeft > 0) s.movesLeft--;
        if (s.movesLeft == 0)
        {
            s.movesLeft = s.maxMovesPerTurn;
            s.current = (s.current == Side.Me) ? Side.Opp : Side.Me;
        }

        // 8) next 반환용
        next = s;
        return undo;
    }
    void Unapply(ref GameState s, in Undo u)
    {
        // 턴/이동/손패/점유 롤백
        s.current = u.prevSide;
        s.movesLeft = u.prevMovesLeft;
        s.handMe = u.prevHandMe;
        s.handOpp = u.prevHandOpp;
        s.occMe = u.prevOccMe;
        s.occOpp = u.prevOccOpp;

        // 공격자 위치 롤백
        ref var me = ref s.pieces[u.pieceId];
        me.sq = u.prevSq;

        // 캡처 복구
        if (u.captured)
        {
            s.pieces[u.capturedId] = u.prevCaptured;
            if (u.prevCaptured.alive)
            {
                if (u.prevCaptured.side == Side.Me) s.occMe |= (1UL << u.prevCaptured.sq);
                else s.occOpp |= (1UL << u.prevCaptured.sq);
            }
        }
    }
    int Evaluate(in GameState s) => 0; // 기존 구현
}
