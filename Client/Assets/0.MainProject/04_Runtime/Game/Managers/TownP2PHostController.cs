using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TownP2PHostController : MonoBehaviour
{
    public static TownP2PHostController Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(TownP2PHostController));
                _instance = go.AddComponent<TownP2PHostController>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    private static TownP2PHostController _instance;
    public static bool HasInstance => _instance != null;

    public bool IsHost { get; private set; }

    private readonly Queue<CS_TownActionRequest> _pending = new();

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

    private void Update()
    {
        if (!IsHost)
            return;

        while (_pending.Count > 0)
        {
            var req = _pending.Dequeue();
            try
            {
                Process(req);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TownP2PHost] Failed to process action actor={req?.ActorId ?? 0}: {ex}");
            }
        }
    }

    public void SetHostMode(bool isHost)
    {
        if (IsHost == isHost)
            return;

        IsHost = isHost;
        if (!IsHost)
            _pending.Clear();

        Debug.Log($"[TownP2PHost] HostMode={IsHost}");
    }

    public void ResetForTownEnd()
    {
        IsHost = false;
        _pending.Clear();
    }

    public void EnqueueLocalActionRequest(CS_TownActionRequest req)
    {
        if (!IsHost || req == null)
            return;

        _pending.Enqueue(req);
    }

    public void EnqueueGuestActionRequest(CS_P2PPayload pkt)
    {
        if (!IsHost || pkt == null || string.IsNullOrWhiteSpace(pkt.Payload))
            return;

        try
        {
            var bytes = Convert.FromBase64String(pkt.Payload);
            var req = new CS_TownActionRequest();
            req.Read(new ArraySegment<byte>(bytes));
            if (pkt.SenderActorId > 0)
                req.ActorId = pkt.SenderActorId;

            _pending.Enqueue(req);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TownP2PHost] Failed to decode guest town action: {ex.Message}");
        }
    }

    private void Process(CS_TownActionRequest req)
    {
        var gs = ClientGameState.Instance;
        if (gs == null || req == null || req.ActorId <= 0)
            return;

        if (!gs.TryGetEntity(req.ActorId, out var entity))
            return;

        var fromX = entity.X;
        var fromY = entity.Y;
        var toX = req.TargetX;
        var toY = req.TargetY;
        var accepted = ValidateTownAction(gs, req, entity, ref toX, ref toY);

        var beatIndex = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;
        if (beatIndex < 0)
            beatIndex = 0;

        var pkt = new SC_TownBeatActions
        {
            BeatIndex = beatIndex
        };
        pkt.beatActionResults.Add(new SC_TownBeatActions.BeatActionResult
        {
            ActorId = req.ActorId,
            ActionKind = req.ActionKind,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Rotation = req.Rotation,
            Accepted = accepted
        });

        var bridge = P2PRelayClientBridge.Instance;
        if (bridge == null || !bridge.IsTownRelayMode)
            return;

        bridge.DispatchLocal(pkt);
        bridge.SendWrappedPacket(pkt);
    }

    private static bool ValidateTownAction(
        ClientGameState gs,
        CS_TownActionRequest req,
        ClientEntityInfo entity,
        ref int toX,
        ref int toY)
    {
        if (req.ActionKind != (int)ActionKind.Move)
            return true;

        var dx = Mathf.Abs(toX - entity.X);
        var dy = Mathf.Abs(toY - entity.Y);
        if (dx + dy != 1)
            return false;

        if (!gs.IsWalkable(toX, toY))
            return false;

        if (gs.IsOccupied(toX, toY, req.ActorId))
            return false;

        return true;
    }
}
