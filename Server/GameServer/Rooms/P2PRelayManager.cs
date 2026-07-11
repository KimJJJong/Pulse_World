using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shared;

public static class P2PRelayManager
{
    private const int MaxCompletedMetricSnapshots = 256;

    private static readonly ConcurrentDictionary<string, P2PRelayRoom> _rooms = new();
    private static readonly ConcurrentQueue<P2PRelayMetricsSnapshot> _completedMetrics = new();

    public static P2PRelayRoom GetOrCreate(string relayId, string mapId, int maxPlayers)
    {
        if (_rooms.TryGetValue(relayId, out var existing))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=GameP2P world={relayId} created=False map={existing.MapId} max={maxPlayers} total={_rooms.Count}");
            return existing;
        }

        var createdRoom = new P2PRelayRoom(relayId, mapId, maxPlayers);
        if (_rooms.TryAdd(relayId, createdRoom))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_create roomType=GameP2P world={relayId} map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_get roomType=GameP2P world={relayId} created=True map={createdRoom.MapId} max={maxPlayers} total={_rooms.Count}");
            return createdRoom;
        }

        var room = _rooms[relayId];

        LogManager.Instance.LogInfo(
            "RoomLifecycle",
            $"event=room_get roomType=GameP2P world={relayId} created=False raceLost=True map={room.MapId} max={maxPlayers} total={_rooms.Count}");
        return room;
    }

    public static bool TryGet(string relayId, [NotNullWhen(true)] out P2PRelayRoom? room)
        => _rooms.TryGetValue(relayId, out room);

    public static void Remove(string relayId)
    {
        if (_rooms.TryRemove(relayId, out var room))
        {
            LogManager.Instance.LogInfo(
                "RoomLifecycle",
                $"event=room_remove roomType=GameP2P world={relayId} map={room.MapId} remaining={_rooms.Count}");
            return;
        }

        LogManager.Instance.LogWarning(
            "RoomLifecycle",
            $"event=room_remove_skip reason=not_found roomType=GameP2P world={relayId} remaining={_rooms.Count}");
    }

    public static IUpdatable[] GetUpdatablesSnapshot()
        => _rooms.Values.Cast<IUpdatable>().ToArray();

    public static P2PRelayRoom[] GetRoomsSnapshot()
        => _rooms.Values.ToArray();

    public static P2PRelayMetricsSnapshot[] GetMetricsSnapshot()
        => _rooms.Values.Select(room => room.GetMetricsSnapshot()).ToArray();

    public static void RecordCompletedMetrics(P2PRelayMetricsSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _completedMetrics.Enqueue(snapshot);

        while (_completedMetrics.Count > MaxCompletedMetricSnapshots
            && _completedMetrics.TryDequeue(out _))
        {
        }
    }

    public static P2PRelayMetricsSnapshot[] DrainCompletedMetricsSnapshot()
    {
        var snapshots = new List<P2PRelayMetricsSnapshot>();
        while (_completedMetrics.TryDequeue(out var snapshot))
            snapshots.Add(snapshot);

        return snapshots.ToArray();
    }
}
