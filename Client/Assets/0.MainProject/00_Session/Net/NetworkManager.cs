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

    // 현재 연결된 Endpoint
    IPEndPoint? _currentEndPoint;

    [SerializeField] float connectTimeoutSeconds = 5f;
    float _deadline = -1f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        //NewSession();
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
        Debug.Log($"[NetworkManager] Request Connect: {endPoint} (Current: {_currentEndPoint}) State: {_state}");

        // 1. 이미 연결된 상태 확인 (Switching 판단)
        if (_state == ConnState.Connecting || _state == ConnState.Connected || _state == ConnState.Ready)
        {
            // 다른 서버로 이동인지 확인
            if (_currentEndPoint != null && !_currentEndPoint.Equals(endPoint))
            {
                Debug.Log($"[NetworkManager] Switching Server detected: {_currentEndPoint} -> {endPoint}. Disconnecting...");
                Disconnect(""); // 기존 연결 종료 (이때 _pending도 초기화됨)
            }
            else
            {
                // 같은 주소 -> Handshake Args만 갱신 후 재진행
                Debug.Log("[NetworkManager] Already connected to same endpoint. Retrying handshake.");
                _pending = new HandshakeArgs(endPoint, ticketId, clientNonce, key);
                TrySendHandshakeOnce();
                return;
            }
        }
        
        // 2. 신규 연결 시작
        // 중요: Disconnect 이후에 _pending을 설정해야 함 (Disconnect가 _pending을 null로 만들기 때문)
        _pending = new HandshakeArgs(endPoint, ticketId, clientNonce, key);
        Debug.Log($"[NetworkManager] Setting Pending Handshake: Ticket={ticketId} Key={key}");

        StartConnect(endPoint);
    }

    public void Disconnect(string reason = "")
    {
        _pending = null;
        _handshakeSent = false;
        _deadline = -1f;
        _state = ConnState.Idle;

        Debug.Log($"[NetworkManager] Disconnect: {reason}");

        try { _session?.Disconnect(); } catch (System.Exception ex) { Debug.LogWarning($"[NetworkManager] Disconnect 중 예외: {ex.Message}"); }
        //NewSession();

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
        Debug.Log($"[NetworkManager] StartConnect: {endPoint}");
        _currentEndPoint = endPoint; 

        NewSession(); // _session = new instance
        var capturedSession = _session; 

        _handshakeSent = false;
        _state = ConnState.Connecting;
        _deadline = Time.unscaledTime + Mathf.Max(1f, connectTimeoutSeconds);

        _connector.Connect(
            endPoint,
            () =>
            {
                capturedSession.OnConnectedAction = () => OnConnectedMainThread(capturedSession);
                capturedSession.OnDisconnectedAction = () => OnDisconnectedMainThread(capturedSession);
                return capturedSession;
            },
            1
        );
    }

    void OnConnectedMainThread(ServerSession session)
    {
        // 1. 세션 검증 (구 세션의 이벤트 무시)
        if (session != _session)
        {
            Debug.Log("[NetworkManager] Ignoring OnConnected from obsolete session.");
            return;
        }

        Debug.Log("[NetworkManager] Connected. Trying Handshake...");
        _state = ConnState.Connected;
        _deadline = -1f;
        TrySendHandshakeOnce();
    }

    void OnDisconnectedMainThread(ServerSession session)
    {
        // 1. 세션 검증 (구 세션의 이벤트 무시)
        if (session != _session)
        {
            Debug.Log("[NetworkManager] Ignoring OnDisconnected from obsolete session.");
            return;
        }

        Debug.Log($"[NetworkManager] Disconnected. PrevState={_state}");

        var wasReady = (_state == ConnState.Ready);

        _state = ConnState.Idle;
        _deadline = -1f;
        _handshakeSent = false;

        // 씬이 판단할 수 있게 이벤트만 발행
        Disconnected?.Invoke();

        // 운영 정석: Ready였다가 끊긴 경우 UI에서 “재접속/로그인 이동” 결정
        // (단, Switching Server 때는 Ready가 아니었거나, Disconnect 호출로 이미 Idle일 수 있음)
        if (wasReady)
            Failed?.Invoke("Disconnected");
    }

    void TrySendHandshakeOnce()
    {
        if (_state != ConnState.Connected && _state != ConnState.Ready) 
        {
            Debug.Log($"[TrySendHandshakeOnce] Fail: State is {_state}");
            return;
        }
        if (_handshakeSent) 
        {
            Debug.Log("[TrySendHandshakeOnce] Fail: Already Sent");
            return;
        }
        if (_pending == null) 
        {
            Debug.LogError("[TrySendHandshakeOnce] Fail: Pending Args is NULL! (Did Disconnect clear it?)");
            return;
        }

        _handshakeSent = true;
        Debug.Log($"[TrySendHandshakeOnce] Sending CS_Handshake... Ticket={_pending.TicketId}");

        // Key는 Game 서버 연결 시에만 사용. Town 연결은 null → 빈 문자열로 처리.
        var p = new CS_Handshake
        {
            ClientNonce = _pending.ClientNonce,
            TicketId = _pending.TicketId,
            Key = _pending.Key ?? string.Empty
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
        if (_state != ConnState.Connected) 
        {
            Debug.Log($"[OnHandshakeSucceeded] Warn: State is {_state} (Expected Connected). Continuing anyway as we received HandshakeOk.");
            // 강제로 Ready로 가도 되는가? 받은거면 된거지.
        }
        _state = ConnState.Ready;
        Debug.Log("[NetworkManager] Handshake Succeeded -> Ready");
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
