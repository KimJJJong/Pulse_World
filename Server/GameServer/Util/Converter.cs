/*using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    public static class Converter
    {
    public static List<Pos> ToPosList(this IEnumerable<CS_RollDicePieceMove.Path> dto)
        => dto == null ? new() : dto.Select(p => new Pos(p.x, p.y)).ToList();

    public static List<CS_RollDicePieceMove.Path> ToPacketPath(this IEnumerable<Pos> path)
        => path == null ? new() : path.Select(p => new CS_RollDicePieceMove.Path { x = p.X, y = p.Y }).ToList();

    public static List<Pos> ToPosList(this IEnumerable<SC_PieceMoveConfirm.Path> dto)
    => dto == null ? new() : dto.Select(p => new Pos(p.x, p.y)).ToList();

    public static List<SC_PieceMoveConfirm.Path> ToPacketPath_Confirm(this IEnumerable<Pos> path)
        => path == null ? new() : path.Select(p => new SC_PieceMoveConfirm.Path { x = p.X, y = p.Y }).ToList();


    public static List<SC_FightConfirm.PiceInfo> ToFightPiceInfos(this IEnumerable<PieceRef> src)
    {
        var list = new List<SC_FightConfirm.PiceInfo>();
        if (src == null) return list;

        foreach (var f in src)
        {
            var dto = new SC_FightConfirm.PiceInfo
            {
                playerSlotNum = f.PlayerSlotNum,
                piceNum = f.PieceType      
            };
            list.Add(dto);
        }
        return list;
    }

    *//*    public static List<PieceRef> ToPacketPieceRefs(this IEnumerable<PieceRef> src)
            => src?.Select(f => new PieceRef{ PlayerSlotNum = f.PlayerSlotNum, PieceType= f.PieceType }).ToList()
               ?? new List<PieceRef>();
    */

/*    public static List<SC_BattleResult.PiceInfo> ToLosePiceInfos(this IEnumerable<PieceRef> losers)
    {
        var list = new List<SC_BattleResult.PiceInfo>();
        if (losers == null) return list;

        foreach (var l in losers)
        {
 
            list.Add(new SC_BattleResult.PiceInfo
            {
                playerSlotNum = l.PlayerSlotNum,
                piceNum = l.PieceType,   

            });
        }
        return list;
    }*//*

    /// <summary>
    /// (신 PDL) 패킷 PieceRef 리스트로 변환 (losePieces 용)
    /// </summary>
    public static List<PieceRef> ToPacketPieceRefs(this IEnumerable<PieceRef> losers)
        => losers?.Select(l => new PieceRef
        { PlayerSlotNum = l.PlayerSlotNum, PieceType = l.PieceType }).ToList()
           ?? new List<PieceRef>();

    /// <summary>
    /// (공용) 도메인 경로(List&lt;Pos&gt;) → 패킷 PathPoint 리스트
    /// </summary>
*//*    public static List<Pos> ToPacketPath(this IEnumerable<Pos> path)
        => path == null ? new() : path.Select(p => new PathPoint { x = p.X, y = p.Y }).ToList();
*//*
    /// <summary>
    /// (레거시/신 공통) 후퇴 플랜 → 패킷 경로 리스트로 변환
    /// 도메인: List&lt;(player,piece,List&lt;Pos&gt; path)&gt;
    /// 패킷:   List&lt;List&lt;PathPoint&gt;&gt;
    /// </summary>
  *//*  public static List<List<PathPoint>> ToPacketRetreatPaths(
        this IEnumerable<(int player, int piece, List<Pos> path)> plans)
        => plans?.Select(pl => pl.path.ToPacketPath()).ToList()
           ?? new List<List<PathPoint>>();*//*


}


*/