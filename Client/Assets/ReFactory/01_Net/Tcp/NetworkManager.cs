using Client;
using ServerCore;
using System;
using System.Net;
using UnityEngine;

public sealed class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; } = null!;

    enum ConnState { Idle, Connecting, Connected }
    ConnState _state = ConnState.Idle;

    readonly Connector _connector = new Connector();
    ServerSession _session = null!;

    HandshakeArgs? _pending;
    bool _handshakeSent;

    [SerializeField] float connectTimeoutSeconds = 5f;
    float _deadline = -1f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        NewSession();
    }

    void Update()
    {
        PumpPackets(256);
        TickTimeout();
    }

    public void ConnectAndHandshake(IPEndPoint endPoint, string ticketId, string clientNonce, string? key = null)
    {
        _pending = new HandshakeArgs(endPoint, ticketId, clientNonce, key);

        if (_state == ConnState.Connecting || _state == ConnState.Connected)
        {
            TrySendHandshakeOnce();
            return;
        }

        StartConnect(endPoint);
    }

    public void Disconnect()
    {
        _pending = null;
        _handshakeSent = false;
        _deadline = -1f;
        _state = ConnState.Idle;

        try { _session.Disconnect(); } catch { }
        NewSession();
    }

    public void Send(ArraySegment<byte> sendBuff)
    {
        if (_state != ConnState.Connected) return;
        _session.Send(sendBuff);
    }

    // --------------------

    void StartConnect(IPEndPoint endPoint)
    {
        NewSession();
        _handshakeSent = false;

        _state = ConnState.Connecting;
        _deadline = Time.unscaledTime + Mathf.Max(1f, connectTimeoutSeconds);

        _connector.Connect(
            endPoint,
            () =>
            {
                _session.OnConnectedAction = OnConnectedMainThread;
                _session.OnDisconnectedAction = OnDisconnectedMainThread;
                return _session;
            },
            1
        );
    }

    void OnConnectedMainThread()
    {
        _state = ConnState.Connected;
        _deadline = -1f;
        TrySendHandshakeOnce();
    }

    void OnDisconnectedMainThread()
    {
        _state = ConnState.Idle;
        _deadline = -1f;
        _handshakeSent = false;

        // 원하면 자동 재연결은 여기서
        // if (_pending != null) StartConnect(_pending.EndPoint);
    }

    void TrySendHandshakeOnce()
    {
        if (_state != ConnState.Connected) return;
        if (_handshakeSent) return;
        if (_pending == null) return;

        _handshakeSent = true;

        // 기존 CS_Handshake 필드에 맞춰 작성
        var p = new CS_Handshake
        {
            clientNonce = _pending.ClientNonce,
            ticketId = _pending.TicketId,
            // key 필드가 있다면 아래 주석 해제
            // key = _pending.Key ?? ""
        };

        _session.Send(p.Write());
    }

    void TickTimeout()
    {
        if (_state != ConnState.Connecting) return;
        if (_deadline < 0) return;
        if (Time.unscaledTime <= _deadline) return;

        Debug.LogWarning("[Network] connect timeout");
        Disconnect();
    }

    void PumpPackets(int maxPerFrame)
    {
        for (int i = 0; i < maxPerFrame; i++)
        {
            var packet = PacketQueue.Instance.Pop();
            if (packet == null) break;
            PacketManager.Instance.HandlePacket(_session, packet);
        }
    }

    void NewSession()
    {
        _session = new ServerSession();
    }

    sealed class HandshakeArgs
    {
        public readonly IPEndPoint EndPoint;
        public readonly string TicketId;
        public readonly string ClientNonce;
        public readonly string? Key;

        public HandshakeArgs(IPEndPoint endPoint, string ticketId, string clientNonce, string? key)
        {
            EndPoint = endPoint;
            TicketId = ticketId;
            ClientNonce = clientNonce;
            Key = key;
        }
    }
}
