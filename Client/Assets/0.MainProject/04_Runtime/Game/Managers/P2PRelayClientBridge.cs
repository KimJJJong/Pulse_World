using ServerCore;
using System;
using UnityEngine;

public sealed class P2PRelayClientBridge : MonoBehaviour
{
    public static P2PRelayClientBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(P2PRelayClientBridge));
                _instance = go.AddComponent<P2PRelayClientBridge>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    private static P2PRelayClientBridge _instance;
    public static bool HasInstance => _instance != null;

    public bool IsRelayMode { get; private set; }
    public bool IsHostLocal { get; private set; }
    public int HostActorId { get; private set; }
    public string RelayKey { get; private set; } = "";
    public bool IsDispatchingLocal { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ConfigureRelay(string ticketKey)
    {
        RelayKey = ticketKey ?? "";
        IsRelayMode = !string.IsNullOrWhiteSpace(RelayKey) &&
                      RelayKey.StartsWith("p2p:", StringComparison.OrdinalIgnoreCase);

        HostActorId = 0;
        IsHostLocal = false;

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        UpdateHostLogWindow();

        if (!IsRelayMode)
            ResetState();
    }

    public void Reset()
    {
        RelayKey = "";
        ResetState();
    }

    private void ResetState()
    {
        IsRelayMode = false;
        HostActorId = 0;
        IsHostLocal = false;

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.ResetForMatchEnd();

        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.ResetMatchState();

        if (P2PHostLogWindow.HasInstance)
            P2PHostLogWindow.Instance.HideAndClear();
    }

    public void SyncHostState()
    {
        if (!IsRelayMode)
            return;

        RefreshHostState();
    }

    public void HandleHostChange(SC_HostChange pkt)
    {
        HostActorId = pkt?.HostActorId ?? 0;
        RefreshHostState();
    }

    public void HandleGuestPayload(CS_P2PPayload pkt)
    {
        if (!IsRelayMode || pkt == null)
            return;

        P2PHostController.Instance.EnqueueGuestActionRequest(pkt);
    }

    public void HandleRelayBroadcast(SC_P2PBroadcast pkt)
    {
        if (pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        try
        {
            var bytes = Convert.FromBase64String(pkt.Payload);
            var session = NetworkManager.Instance != null ? NetworkManager.Instance.CurrentSession : null;
            if (session == null)
                return;

            PacketManager.Instance.OnRecvPacket(session, new ArraySegment<byte>(bytes));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PRelayClientBridge] Failed to decode broadcast: {ex.Message}");
        }
    }

    public void SendWrappedPacket(IPacket packet)
    {
        if (packet == null || NetworkManager.Instance == null)
            return;

        if (!IsRelayMode)
        {
            NetworkManager.Instance.Send(packet.Write());
            return;
        }

        var payload = Encode(packet);
        if (string.IsNullOrEmpty(payload))
            return;

        NetworkManager.Instance.Send(new CS_P2PPayload
        {
            SenderActorId = SessionContext.Instance.MyActorId,
            Payload = payload
        }.Write());
    }

    public void DispatchLocal(IPacket packet)
    {
        if (packet == null || NetworkManager.Instance == null)
            return;

        var session = NetworkManager.Instance.CurrentSession;
        if (session == null)
            return;

        try
        {
            IsDispatchingLocal = true;
            PacketManager.Instance.HandlePacket(session, packet);
        }
        finally
        {
            IsDispatchingLocal = false;
        }
    }

    private void RefreshHostState()
    {
        if (!IsRelayMode)
        {
            HostActorId = 0;
            IsHostLocal = false;
            if (P2PHostController.HasInstance)
                P2PHostController.Instance.SetHostMode(false);

            UpdateHostLogWindow();
            return;
        }

        IsHostLocal = HostActorId > 0 && SessionContext.Instance.MyActorId == HostActorId;
        P2PHostController.Instance.SetHostActorId(HostActorId);
        P2PHostController.Instance.SetHostMode(IsHostLocal);
        if (P2PContentDirector.HasInstance)
            P2PContentDirector.Instance.OnHostLocalChanged(IsHostLocal);
        UpdateHostLogWindow();
    }

    private void UpdateHostLogWindow()
    {
        if (!IsRelayMode)
        {
            if (P2PHostLogWindow.HasInstance)
                P2PHostLogWindow.Instance.HideAndClear();

            return;
        }

        P2PHostLogWindow.Instance.SetRelayContext(RelayKey, IsRelayMode, IsHostLocal, HostActorId);
    }

    private static string Encode(IPacket pkt)
    {
        var segment = pkt.Write();
        if (segment.Array == null || segment.Count <= 0)
            return "";

        return Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
    }
}
