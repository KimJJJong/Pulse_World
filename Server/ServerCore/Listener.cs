using System;
using System.Net;
using System.Net.Sockets;
using Shared;

namespace ServerCore
{
    public class Listener
    {
        Socket _listenSocket;
        Func<PacketSession> _sessionFactory;

        /// <summary>
        /// Init ListenerSocket on the Server
        /// </summary>
        public void Init(IPEndPoint endPoint, Func<PacketSession> sessionFactory, int register = 10, int backlog = 100)
        {
            _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _sessionFactory += sessionFactory;

            // [RTT Fix] 서버 소켓 재사용 - 재시작 시 즉시 바인딩 가능
            _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // [RTT Fix] 수신 버퍼 확대 (기본 8KB -> 64KB)
            // 고빈도 패킷 환경에서 OS 수신 버퍼 부족으로 인한 드롭 방지
            _listenSocket.ReceiveBufferSize = 65536;
            _listenSocket.SendBufferSize    = 65536;

            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(backlog);

            for (int i = 0; i < register; i++)
            {
                var args = new SocketAsyncEventArgs();
                args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
                RegisterAccept(args);
            }
        }

        void RegisterAccept(SocketAsyncEventArgs args)
        {
            args.AcceptSocket = null;

            bool pending = _listenSocket.AcceptAsync(args);
            if (pending == false)
                OnAcceptCompleted(null, args);
        }

        void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            try
            {
                if (args.SocketError == SocketError.Success)
                {
                    var clientSocket = args.AcceptSocket;

                    // [RTT Fix] Accept된 클라이언트 소켓에도 옵션 적용
                    // Session.Start()에서 NoDelay를 설정하지만, 혹시 몰라 여기서도 명시
                    clientSocket.NoDelay           = true;   // Nagle 비활성화
                    clientSocket.ReceiveBufferSize  = 65536;  // OS 수신 버퍼 64KB
                    clientSocket.SendBufferSize     = 65536;  // OS 송신 버퍼 64KB

                    // [RTT Fix] Keep-Alive: 유령 연결 조기 감지 (30초 idle 후 탐지 시작, 5초 간격 3회)
                    clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    Session session = _sessionFactory.Invoke();
                    session.Start(clientSocket);
                    session.OnConnected(clientSocket.RemoteEndPoint);

                    LogManager.Instance.LogInfo("Listener", $"Accepted {clientSocket.RemoteEndPoint}");
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.LogError("Listener", $"AcceptSocket Error: {e}");
            }
            finally
            {
                RegisterAccept(args);
            }
        }
    }
}
