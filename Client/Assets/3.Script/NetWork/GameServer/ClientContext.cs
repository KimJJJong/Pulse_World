//using System;
//using UnityEngine;

///// <summary>
///// 클라이언트 런타임 컨텍스트(세션 상태) 보관소.
///// - 범용적으로 Lobby/GS/Match/Player/Runtime 묶어서 보관
///// - 어디서나 ClientContext.Current 로 접근
///// </summary>
//public sealed class ClientContext
//{
//    public static ClientContext Current => _current ??= new ClientContext();
//    static ClientContext _current;

//    private ClientContext() { }

//    // ========== 1) 로비에서 받은 정보 ==========
//    public LobbyInfo Lobby { get; private set; } = new();
//    [Serializable]
//    public class LobbyInfo
//    {
//        public string LobbyUserId;          // 로그인/프로필 식별자 (선택)
//        public string RoomId;               // 방 ID
//        public string MatchId;              // 매치 ID (있을 경우)
//        public int ProtoVerFromLobby;    // 로비가 알려준 protoVer
//        public long LobbyServerTimeMs;    // game.begin 당시 서버시각 (초기 오프셋 계산용)
//    }

//    // ========== 2) 게임서버 접속 파라미터 ==========
//    public GameServerInfo GS { get; private set; } = new();
//    [Serializable]
//    public class GameServerInfo
//    {
//        public string Host;
//        public int Port;
//        public string Ticket;               // RS256
//        public int ProtoVer;             // 클라가 사용할 proto(보통 상수 NetConfig.ProtoVer)
//        public int TickRateHint;         // (선택) 로비/티켓에 있었다면 힌트
//    }

//    // ========== 3) 매치/플레이어 정보 ==========
//    public MatchInfo Match { get; private set; } = new();
//    [Serializable]
//    public class MatchInfo
//    {
//        public string MatchId;
//        public string RoomId;
//        public string MyUid;
//        public char MySide;               // 'A' | 'B' (아직 모르면 '?')
//        public string OppUid;               // 상대 UID (선택)
//    }

//    // ========== 4) 런타임 상태 ==========
//    public RuntimeInfo Runtime { get; private set; } = new();
//    [Serializable]
//    public class RuntimeInfo
//    {
//        public bool ConnectedToGS;
//        public bool SentLoaded;
//        public bool AllPlayersLoaded;
//        public int EffectiveTickRate;      // S_Welcome에서 확정
//        public long StartAtMs;              // SC_GameBegin에서 확정
//        public int StartTick;              // SC_GameBegin에서 확정
//    }

//    // ------------------ 조작 API ------------------

//    /// <summary> game.begin 수신 시 컨텍스트 채우기 </summary>
//    public void ApplyGameBegin(
//        string lobbyUserId,
//        string roomId,
//        string matchId,
//        string host, int port,
//        string ticket,
//        int lobbyProtoVer,
//        long lobbyServerTimeMs,
//        int tickRateHint = -1)
//    {
//        Lobby = new LobbyInfo
//        {
//            LobbyUserId = lobbyUserId,
//            RoomId = roomId,
//            MatchId = matchId,
//            ProtoVerFromLobby = lobbyProtoVer,
//            LobbyServerTimeMs = lobbyServerTimeMs
//        };
//        GS = new GameServerInfo
//        {
//            Host = host,
//            Port = port,
//            Ticket = ticket,
//           // ProtoVer = NetConfig.ProtoVer,     // 클라 상수 (서버 요구와 동일해야 함)
//            TickRateHint = tickRateHint
//        };
//        Match = new MatchInfo
//        {
//            MatchId = matchId,
//            RoomId = roomId,
//            MyUid = lobbyUserId,               // 프로젝트에 맞게 매핑
//            MySide = '?'                       // S_Welcome에서 확정 가능
//        };
//        Runtime = new RuntimeInfo();           // 리셋
//        //TimeSync.SetOffsetFromServerNow(lobbyServerTimeMs);
//    }

//    /// <summary> GS 연결 성공시 호출 </summary>
//    public void MarkConnectedToGs() => Runtime.ConnectedToGS = true;

//    /// <summary> S_Welcome 수신 시 확정값 갱신 </summary>
//    public void ApplyWelcome(string side, int tickRate, long serverTimeMs, string map = null, int seed = 0)
//    {
//        if (!string.IsNullOrEmpty(side))
//            Match.MySide = side[0];
//        Runtime.EffectiveTickRate = tickRate;
//        //TimeSync.SetOffsetFromServerNow(serverTimeMs); // 정밀 재보정
//        // map/seed를 보관하고 싶으면 별도 필드 만들어 추가
//    }

//    /// <summary> 로딩 완료 보고 후 </summary>
//    public void MarkLoadedSent() => Runtime.SentLoaded = true;

//    /// <summary> 모두 로드 완료 알림 </summary>
//    public void MarkAllPlayersLoaded() => Runtime.AllPlayersLoaded = true;

//    /// <summary> 시작 예약 수신 </summary>
//    public void ApplyGameBeginSchedule(long startAtMs, int startTick)
//    {
//        Runtime.StartAtMs = startAtMs;
//        Runtime.StartTick = startTick;
//    }

//    /// <summary> 세션 초기화(로비로 복귀 시) </summary>
//    public void Reset()
//    {
//        Lobby = new LobbyInfo();
//        GS = new GameServerInfo();
//        Match = new MatchInfo();
//        Runtime = new RuntimeInfo();
//        TimeSync.Reset();
//    }
//}
