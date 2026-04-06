using System;
using System.Net;
using System.Net.Sockets;
using Shared;

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
                _sessionFactory = sessionFactory;

                // [RTT Fix] 클라이언트 소켓 옵션 - Session.Start() 전에 설정해야 유효
                socket.NoDelay          = true;   // Nagle 비활성화: 소형 패킷 즉시 전송
                socket.ReceiveBufferSize = 65536;  // OS 수신 버퍼 64KB
                socket.SendBufferSize    = 65536;  // OS 송신 버퍼 64KB

                var args = new SocketAsyncEventArgs();
                args.Completed    += OnConnectCompleted;
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
            try
            {
                if (args.SocketError == SocketError.Success)
                {
                    Session session = _sessionFactory.Invoke();
                    session.Start(args.ConnectSocket);
                    session.OnConnected(args.RemoteEndPoint);
                    LogManager.Instance.LogInfo("Connector", "Connected to server!");
                }
                else
                {
                    Console.WriteLine($"OnConnectCompleted Fail: {args.SocketError}");
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.LogError("Connector", $"Connect failed: {args.SocketError}");
                Console.WriteLine($"Err during Connect: {e.Message}");
            }
        }
    }
}
