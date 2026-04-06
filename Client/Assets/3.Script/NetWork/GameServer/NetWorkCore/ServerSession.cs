using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using ServerCore;
using UnityEngine;
using System.Collections;
using System.Threading.Tasks;

namespace Client
{
    class ServerSession : PacketSession
    {
        public Action OnConnectedAction;
        public Action OnDisconnectedAction;
        public Action<IPacket> OnRecvPacketAction;

        public override async void OnConnected(EndPoint endPoint)
        {
            MainThreadDispatcher.Post(() =>
            {
                UnityEngine.Debug.Log($"[Client] Connected {endPoint}");
                OnConnectedAction?.Invoke();
            });
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            MainThreadDispatcher.Post(() =>
            {
                Debug.Log($"[Client] Disconnected {endPoint}");
                OnDisconnectedAction?.Invoke();
            });
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer, (s, p) =>
            {
                // ============================================================
                // [RTT Fix] 3단 처리 경로
                //
                // ① 소켓 스레드 직접 처리 (Thread-Safe 순수 계산만):
                //    - SC_Pong : RTT측정 + TimeSync 오프셋 갱신 (잠금 없음, 즉시)
                //
                // ② MainThreadDispatcher.PostImmediate (우선순위 큐):
                //    - SC_BeatSync       : SongStart 보정 → 같은 프레임 최우선 반영
                //    - SC_BeatActions    : 판정 결과 → 즉각 UI 처리
                //    - SC_BeatTelegraphs : 예고 타이밍 → 프레임 선두에서 처리
                //    - SC_TownBeatActions: 마을 비트 액션
                //    Update()에서 일반 PacketQueue보다 먼저 소비됩니다.
                //
                // ③ PacketQueue (일반 큐 → NetworkManager.Update() 처리):
                //    - 씬 오브젝트 생성/삭제, 스폰, 초기화 등
                //    - 16~33ms 딜레이 허용 가능한 패킷
                // ============================================================

                if (p is SC_Pong)
                {
                    // ① 소켓 스레드 즉시 처리 (PingManager.OnPong은 Interlocked/순수계산)
                    PacketManager.Instance.HandlePacket(s, p);
                }
                else if (p is SC_BeatSync
                      || p is SC_BeatActions
                      || p is SC_BeatTelegraphs
                      || p is SC_TownBeatActions
                      || p is SC_ActionInstantBroadcast
                      || p is SC_CancelAction)
                {
                    // ② 우선순위 큐 - Update() 프레임 선두에서 먼저 처리
                    MainThreadDispatcher.PostImmediate(() => PacketManager.Instance.HandlePacket(s, p));
                }
                else
                {
                    // ③ 일반 큐 - 씬/오브젝트 조작이 필요한 패킷
                    PacketQueue.Instance.Push(p);
                }
            });
        }

        public override void OnSend(int numOfBytes)
        {
            // Debug.Log($"Transferred bytes: {numOfBytes}");
        }
    }
}
