//using System;
//using System.Collections.Concurrent;

//public static class TownEnterHandler
//{
//    // 간단 슬롯 발급기(프로세스 단위). 
//    // 운영에서는 townId별로 발급하거나, 재접속 시 uid->slot 고정 정책도 가능.
//    static int _nextSlot = 1;

//    // uid -> slot 고정(선택). 재접속/중복 방지에 매우 유리
//    static readonly ConcurrentDictionary<string, int> _uidToSlot = new();

//    public static void Handle(ClientSession s, CS_TownEnter p)
//    {
//        // 1) 핸드셰이크로 UID가 확정됐는지 체크
//        if (string.IsNullOrEmpty(s.Uid))
//        {
//            s.Send(new SC_Warn { code = 3101, msg = "UID_NOT_SET_HANDSHAKE_REQUIRED" }.Write());
//            return;
//        }

//        // 2) TownId 결정
//        // - 지금은 단일 Town이면 "Town_01" 고정
//        // - 나중에 채널/샤드/지역이면 여기서 결정
//        string townId = "Town_01";

//        // 3) slot 할당 정책
//        // (A) uid 고정: 같은 유저는 항상 같은 slot (재접속 안정)
//        int slot = _uidToSlot.GetOrAdd(s.Uid, _ => System.Threading.Interlocked.Increment(ref _nextSlot));

//        // 4) 기존에 다른 Town에 붙어있다면 정리(선택)
//        // - session이 "현재 TownId"를 들고 있다면, 바뀔 때 기존 Unbind
//        if (!string.IsNullOrEmpty(s.Key) && s.Key != townId)
//        {
//            if (TownManager.TryGet(s.Key, out var oldTown))
//                oldTown.Unbind(s);
//        }
//        ///////////////////////////////////////!!!!!!!!!!!!!!!!!!!! 일단 RoomId/ TwonId 모두 Key로 사용중 TODO :
//        // 5) 바인딩
//        var town = TownManager.GetOrCreate(townId);

//        // slot 충돌(다른 세션이 이미 같은 slot) 방지:
//        // 여기서 true/false로 막아주는 게 TownRoom.Bind가 이미 해줌
//        if (!town.Bind(slot, s))
//        {
//            s.Send(new SC_Warn { code = 3102, msg = "TOWN_BIND_FAILED_SLOT_CONFLICT" }.Write());
//            return;
//        }

//        // 6) 세션에 상태 기록 (중요: 라우팅/정리용)
//        s.Key = townId;

//        // TownRoom.Bind 내부에서:
//        // - StartTownIfNeeded()
//        // - _session.SendInitPacketToPlayer(s) (Enqueue)
//        // 까지 이미 진행됨.
//    }
//}
