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
            Debug.Log($"OnDisconnected : {endPoint}");
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            //Debug.Log("RecvPack");
            PacketManager.Instance.OnRecvPacket(this, buffer, (s,p)=> PacketQueue.Instance.Push(p));
        }

        public override void OnSend(int numOfBytes)
        {
            //Debug.Log($"Transferred bytes: {numOfBytes}");
        }
    }
}
