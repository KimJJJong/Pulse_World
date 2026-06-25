using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared;

namespace ServerCore
{
	public class Connector
	{
		Func<Session> _sessionFactory;

		public void Connect(IPEndPoint endPoint, Func<Session> sessionFactory, int count =1 )
		{
			for(int i = 0; i < count; i++)
			{
			// 휴대폰 설정
			Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			_sessionFactory = sessionFactory;

			SocketAsyncEventArgs args = new SocketAsyncEventArgs();
			args.Completed += OnConnectCompleted;
			args.RemoteEndPoint = endPoint;
			args.UserToken = socket;

			RegisterConnect(args);

			}
		}

		void RegisterConnect(SocketAsyncEventArgs args)
		{
			Socket socket = args.UserToken as Socket;	
			if (socket == null)
				return;

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
                    session.Start(args.ConnectSocket); // args.UserToken as Socket : 두가지 방법
                    session.OnConnected(args.RemoteEndPoint);
                    LogManager.Instance.LogInfo("Connector", "Connected to server!");
                }
                else
                {
                    // [ping-fix] Console.WriteLine → LogManager (비동기 큐)
                    LogManager.Instance.LogError("Connector", $"OnConnectCompleted Fail: {args.SocketError}");
                }
            }
			catch(Exception e)
			{
                // [ping-fix] 동일 이벤트에 대해 로그 한 번만. Console.WriteLine 제거.
                LogManager.Instance.LogError("Connector", $"Connect failed: {args.SocketError} / {e.Message}");
			}
		}
	}
}
