using Client;
using ServerCore;
using System;
using System.Net;
using UnityEngine;

public sealed class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; } = null!;

    enum ConnState { Idle, Connecting, Connected, Ready }
    ConnState _state = ConnState.Idle;

    public bool IsReady => _state == ConnState.Ready;
    public bool IsConnected => _state == ConnState.Connected || _state == ConnState.Ready;

    // 정석: Network는 “신호만 발행”
    public event Action? Ready;             // Handshake OK
    public event Action? Disconnected;      // 끊김
    public event Action<string>? Failed;    // HandshakeFail/Timeout 등

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

    /// <summary>
    /// Connect(TCP) -> CS_Handshake(1회) 까지.
    /// OK는 SC_HandshakeOk에서 OnHandshakeSucceeded() 호출로 확정됨.
    /// </summary>
    public void ConnectAndHandshake(IPEndPoint endPoint, string ticketId, string clientNonce, string? key = null)
    {
        _pending = new HandshakeArgs(endPoint, ticketId, clientNonce, key);

        if (_state == ConnState.Connecting || _state == ConnState.Connected || _state == ConnState.Ready)
        {
            TrySendHandshakeOnce();
            return;
        }

        StartConnect(endPoint);
    }

    public void Disconnect(string reason = "")
    {
        _pending = null;
        _handshakeSent = false;
        _deadline = -1f;
        _state = ConnState.Idle;

        try { _session.Disconnect(); } catch { }
        NewSession();

        if (!string.IsNullOrEmpty(reason))
            Failed?.Invoke(reason);
    }

    public void Send(ArraySegment<byte> sendBuff)
    {
        if (!IsConnected) return;
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
        var wasReady = (_state == ConnState.Ready);

        _state = ConnState.Idle;
        _deadline = -1f;
        _handshakeSent = false;

        // 씬이 판단할 수 있게 이벤트만 발행
        Disconnected?.Invoke();

        // 운영 정석: Ready였다가 끊긴 경우 UI에서 “재접속/로그인 이동” 결정
        if (wasReady)
            Failed?.Invoke("Disconnected");
    }

    void TrySendHandshakeOnce()
    {
        if (_state != ConnState.Connected && _state != ConnState.Ready) return;
        if (_handshakeSent) return;
        if (_pending == null) return;

        _handshakeSent = true;

        var p = new CS_Handshake
        {
            ClientNonce = _pending.ClientNonce,
            TicketId = _pending.TicketId,
            // key 필드가 있다면 주석 해제
             Key = _pending.Key ?? "TestKey"
        };

        _session.Send(p.Write());
    }

    void TickTimeout()
    {
        if (_state != ConnState.Connecting) return;
        if (_deadline < 0) return;
        if (Time.unscaledTime <= _deadline) return;

        Debug.LogWarning("[Network] connect timeout");
        Disconnect("ConnectTimeout");
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

    void NewSession() => _session = new ServerSession();

    // ====== Handshake 결과는 “패킷 핸들러”에서 호출 ======

    public void OnHandshakeSucceeded()
    {
        if (_state != ConnState.Connected && _state != ConnState.Ready) return;

        _state = ConnState.Ready;
        Ready?.Invoke();
    }

    public void OnHandshakeFailed(string reason = "HandshakeFail")
    {
        Disconnect(reason);
    }

    public void OnForcedDisconnect(string reason = "ForcedDisconnect")
    {
        Disconnect(reason);
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
