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
            try
            {
                PacketManager.Instance.OnRecvPacket(this, buffer, (s,p) =>
                {
                    // [Optimization] Ping/Pong은 유니티 프레임(Update) 딜레이를 피해 소켓 수신 스레드에서 즉각 처리하여 Jitter를 0으로 만듭니다.
                    if (p is SC_Pong)
                    {
                        PacketManager.Instance.HandlePacket(s, p);
                    }
                    else
                    {
                        PacketQueue.Instance.Push(p);
                    }
                });
            }
            catch (Exception ex)
            {
                ushort packetId = 0;
                if (buffer.Array != null && buffer.Count >= 4)
                    packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);

                MainThreadDispatcher.Post(() =>
                {
                    Debug.LogError($"[ServerSession] Packet parse failed id={packetId} size={buffer.Count} err={ex}");
                });
                throw;
            }
        }

        public override void OnSend(int numOfBytes)
        {
            //Debug.Log($"Transferred bytes: {numOfBytes}");
        }
    }
}
