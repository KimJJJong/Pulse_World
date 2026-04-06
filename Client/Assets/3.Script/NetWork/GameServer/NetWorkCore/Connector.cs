using System;
using System.Net;
using System.Net.Sockets;

namespace ServerCore
{
    public class Connector
    {
        Func<Session> _sessionFactory;

        public void Connect(IPEndPoint endPoint, Func<Session> sessionFactory, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                // [RTT Fix] 소켓 옵션 - Session.Start() 전에 설정해야 유효
                socket.NoDelay           = true;   // Nagle 비활성화: CS_Ping 등 소형 패킷 즉시 전송
                socket.ReceiveBufferSize  = 65536;  // OS 수신 버퍼 64KB (기본 8KB -> 고빈도 환경 대응)
                socket.SendBufferSize     = 65536;  // OS 송신 버퍼 64KB

                _sessionFactory = sessionFactory;

                var args = new SocketAsyncEventArgs();
                args.Completed     += OnConnectCompleted;
                args.RemoteEndPoint = endPoint;
                args.UserToken      = socket;

                RegisterConnect(args);
            }
        }

        void RegisterConnect(SocketAsyncEventArgs args)
        {
            var socket = args.UserToken as Socket;
            if (socket == null) return;

            bool pending = socket.ConnectAsync(args);
            if (pending == false)
                OnConnectCompleted(null, args);
        }

        void OnConnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                Session session = _sessionFactory.Invoke();
                session.Start(args.ConnectSocket);
                session.OnConnected(args.RemoteEndPoint);
            }
            else
            {
                Console.WriteLine($"OnConnectCompleted Fail: {args.SocketError}");
            }
        }
    }
}
