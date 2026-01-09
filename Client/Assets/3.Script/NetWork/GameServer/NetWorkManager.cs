using Client;
using ServerCore;
using System;
using System.Net;
using UnityEngine;

public class NetWorkManager : MonoBehaviour
{
    public static NetWorkManager Instance { get; private set; } = null!;

    // ---- dependencies (existing) ----
    readonly Connector _connector = new Connector();
    ServerSession _session = new ServerSession();

    // ---- state ----
    enum ConnState { Idle, Connecting, Connected, Closing }
    ConnState _state = ConnState.Idle;

    // ---- pending handshake ----
    HandshakeContext? _pending;
    bool _handshakeSent;

    // ---- connect timeout ----
    float _connectDeadline = -1f;

    [Header("Connect")]
    [SerializeField] float connectTimeoutSeconds = 5f;
    [SerializeField] int connectRetryCount = 1;


    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResetSession();
    }

    void Update()
    {
        PumpPackets(maxPerFrame: 256);
        TickTimeout();
    }
    // =========================
    // Public API
    // =========================

    /// <summary>
    /// endpoint로 연결 후, CS_Handshake를 1회 보낸다.
    /// (ticketId 필수, key는 서버가 요구하면 넣어 확장)
    /// </summary>
    public void ConnectAndHandshake(IPEndPoint endpoint, string ticketId, string clientNonce)
    {
        // 중복 연결/요청 방지 정책: 연결 중이면 "최신 요청으로 교체"만 하고 대기
        _pending = new HandshakeContext(endpoint, ticketId, clientNonce);

        if (_state == ConnState.Connecting || _state == ConnState.Connected)
        {
            // 이미 연결된 상태면 즉시 핸드셰이크 시도(단, 1회 보장)
            TrySendHandshakeOnce();
            return;
        }

        StartConnect(endpoint);
    }

    public void Disconnect()
    {
        _pending = null;
        _handshakeSent = false;

        if (_state == ConnState.Idle) return;
        _state = ConnState.Closing;

        try { _session.Disconnect(); } catch { }
        ResetSession();

        _state = ConnState.Idle;
    }

    public void Send(ArraySegment<byte> sendBuff)
    {
        if (_state != ConnState.Connected) return;
        _session.Send(sendBuff);
    }

    // =========================
    // Internals
    // =========================

    void StartConnect(IPEndPoint endpoint)
    {
        ResetSession();
        _handshakeSent = false;
        _state = ConnState.Connecting;

        _connectDeadline = Time.unscaledTime + Mathf.Max(1f, connectTimeoutSeconds);

        _connector.Connect(
            endpoint,
            () =>
            {
                // Connector가 WorkerThread에서 호출할 가능성이 있으니
                // 여기서는 세션만 반환하고, 핸드셰이크는 MainThread에서 처리
                _session.OnConnectedAction = OnConnectedMainThread;
                _session.OnDisconnectedAction = OnDisconnectedMainThread;
                return _session;
            },
            connectRetryCount
        );
    }

    void OnConnectedMainThread()
    {
        if (_state == ConnState.Closing) return;

        _state = ConnState.Connected;
        _connectDeadline = -1f;

        TrySendHandshakeOnce();
    }

    void OnDisconnectedMainThread()
    {
        // 서버/네트워크 끊김
        _connectDeadline = -1f;
        _handshakeSent = false;

        if (_state == ConnState.Closing)
        {
            _state = ConnState.Idle;
            return;
        }

        _state = ConnState.Idle;

        // 필요하면 자동 재연결 정책을 여기서 넣으면 됨
        // if (_pending != null) StartConnect(_pending.Endpoint);
    }

    void TrySendHandshakeOnce()
    {
        if (_state != ConnState.Connected) return;
        if (_handshakeSent) return;
        if (_pending == null) return;

        _handshakeSent = true;

        // 서버 규격에 맞춰 CS_Handshake 작성
        var p = new CS_Handshake
        {
            clientNonce = _pending.ClientNonce,
            ticketId = _pending.TicketId,
            // TODO: server가 key 요구하면 여기에 추가
            // key = _pending.Key
        };

        _session.Send(p.Write());
    }

    void TickTimeout()
    {
        if (_state != ConnState.Connecting) return;
        if (_connectDeadline < 0) return;

        if (Time.unscaledTime > _connectDeadline)
        {
            Debug.LogWarning("[Network] connect timeout");
            Disconnect();
        }
    }

    void PumpPackets(int maxPerFrame)
    {
        if (_state != ConnState.Connected && _state != ConnState.Connecting)
            return;

        for (int i = 0; i < maxPerFrame; i++)
        {
            var packet = PacketQueue.Instance.Pop();
            if (packet == null) break;

            PacketManager.Instance.HandlePacket(_session, packet);
        }
    }

    void ResetSession()
    {
        // 세션을 새로 만들어야 OnConnectedAction/OnDisconnectedAction 같은 콜백이 깔끔히 초기화됨
        _session = new ServerSession();
    }

    sealed class HandshakeContext
    {
        public readonly IPEndPoint Endpoint;
        public readonly string TicketId;
        public readonly string ClientNonce;

        public HandshakeContext(IPEndPoint endpoint, string ticketId, string clientNonce)
        {
            Endpoint = endpoint;
            TicketId = ticketId;
            ClientNonce = clientNonce;
        }
    }
}