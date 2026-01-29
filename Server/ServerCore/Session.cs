using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Shared;

namespace ServerCore
{

    public abstract class PacketSession : Session
    {
        public int SessionID { get; set; }
        //public string MatchId { get; set; }
        public virtual string CurrentWorldId { get; set; }

        public static readonly int HeaderSize = 2;

        // [size(2)][packetId(2)][ ... ][size(2)][packetId(2)][ ... ]
        public sealed override int OnRecv(ArraySegment<byte> buffer)
        {
            // 처리한 Byte 크기
            int processLen = 0;
            // 얼만치 쌓아 보냈는감?
            int packetCount = 0;

            while (true)
            {
                // 최소한 헤더는 파싱할 수 있는지 확인 : 대가리보다 작으면 안되지 암
                if (buffer.Count < HeaderSize)
                    break;

                // 패킷이 완전체로 도착했는지 확인
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
                if (buffer.Count < dataSize)
                    break;

                // 여기까지 왔으면 패킷 조립 가능 : DeSerialization
                OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));
                packetCount++;

                processLen += dataSize;
                buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
            }

            if (packetCount > 1)
                Console.WriteLine($"패킷 모아 보내기 : {packetCount}");

            return processLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);
    }

    public abstract class Session
    {
        Socket _socket;
        int _disconnected = 0;  // Session Connect Condition Flag

        RecvBuffer _recvBuffer = new RecvBuffer(65535); //TODO Need to Size Adjust

        object _lock = new object();
        Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>();
        List<ArraySegment<byte>> _pendingList = new List<ArraySegment<byte>>();
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();

        public abstract void OnConnected(EndPoint endPoint);
        public abstract int OnRecv(ArraySegment<byte> buffer);
        public abstract void OnSend(int numOfBytes);
        public abstract void OnDisconnected(EndPoint endPoint);

        void Clear()
        {
            lock (_lock)
            {
                _sendQueue.Clear();
                _pendingList.Clear();
            }
        }

        public void Start(Socket socket)
        {
            _socket = socket;
            _socket.NoDelay = true;
            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

            LogManager.Instance.LogInfo("Session", "Session Started");
            RegisterRecv();
        }

        public void Send(List<ArraySegment<byte>> sendBufferList)
        {
            if (sendBufferList.Count == 0) return;

            lock (_lock)
            {
                foreach (ArraySegment<byte> sendBuffer in sendBufferList)
                {
                    _sendQueue.Enqueue(sendBuffer);

                    if (_pendingList.Count == 0)
                        RegisterSend();
                }
            }
        }

        public void Send(ArraySegment<byte> sendBuff)
        {
            lock (_lock)
            {
                _sendQueue.Enqueue(sendBuff);
                if (_pendingList.Count == 0)
                    RegisterSend();
            }
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1) return;

            EndPoint ep = _socket.RemoteEndPoint;
            LogManager.Instance.LogInfo("Session", $"Disconnecting session from {ep}");

            OnDisconnected(ep);
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            Clear();
        }

        #region 네트워크 통신

        void RegisterSend()
        {
            if (_disconnected == 1) return;

            while (_sendQueue.Count > 0)
            {
                ArraySegment<byte> buff = _sendQueue.Dequeue();
                _pendingList.Add(buff);
            }
            _sendArgs.BufferList = _pendingList;

            try
            {
                bool pending = _socket.SendAsync(_sendArgs);
                if (pending == false)
                    OnSendCompleted(null, _sendArgs);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError("Session", $"RegisterSend Failed : {ex}");
            }
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
                {
                    try
                    {
                        _sendArgs.BufferList = null;
                        _pendingList.Clear();

                        OnSend(_sendArgs.BytesTransferred);

                        if (_sendQueue.Count > 0)
                            RegisterSend();
                    }
                    catch (Exception e)
                    {
                        LogManager.Instance.LogError("Session", $"OnSendCompleted Failed {e}");
                    }
                }
                else
                {
                    Disconnect();
                }
            }
        }

        void RegisterRecv()
        {
            if (_disconnected == 1) return;

            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            _recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            try
            {
                bool pending = _socket.ReceiveAsync(_recvArgs);
                if (pending == false)
                    OnRecvCompleted(null, _recvArgs);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError("Session", $"RegisterRecv Failed : {ex}");
            }
        }

        void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        LogManager.Instance.LogError("Session", "OnWrite Failed");
                        Disconnect();
                        return;
                    }

                    int processLen = OnRecv(_recvBuffer.ReadSegment);
                    if (processLen < 0 || _recvBuffer.DataSize < processLen)
                    {
                        LogManager.Instance.LogError("Session", $"OnRecv ProcessLen Error: {processLen}, DataSize: {_recvBuffer.DataSize}");
                        Disconnect();
                        return;
                    }

                    if (_recvBuffer.OnRead(processLen) == false)
                    {
                        LogManager.Instance.LogError("Session", "OnRead Failed");
                        Disconnect();
                        return;
                    }

                    RegisterRecv();
                }
                catch (Exception e)
                {
                    LogManager.Instance.LogError("Session", $"OnRecvCompleted Exception: {e}");
                }
            }
            else
            {
                LogManager.Instance.LogInfo("Session", $"OnRecvCompleted Disconnect Signal: {args.SocketError}, Bytes: {args.BytesTransferred}");
                Disconnect();
            }
        }

        #endregion
    }
}