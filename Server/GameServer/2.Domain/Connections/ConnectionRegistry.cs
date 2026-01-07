using Server.Infrastructure.ControlPlaneClient;
using Server.Presentation.Tcp.PacketHandlers;
using System;
using System.Collections.Concurrent;

namespace Server.Domain.Connections;

/// <summary>
/// uid -> active connection 저장.
/// epoch 기반으로 최신 연결만 인정.
/// </summary>
public sealed class ConnectionRegistry : IConnectionKicker
{
    private sealed class Entry
    {
        public ITcpConnection Conn { get; init; } = default!;
        public long Epoch { get; init; }
        public DateTimeOffset BoundAt { get; init; }
    }

    private readonly ConcurrentDictionary<string, Entry> _byUid = new();

    /// <summary>
    /// 핸드셰이크 성공 후 호출.
    /// - uid가 이미 있으면 "epoch 비교"로 결정.
    /// </summary>
    public void Bind(string uid, long epoch, ITcpConnection conn)
    {
        var newEntry = new Entry
        {
            Conn = conn,
            Epoch = epoch,
            BoundAt = DateTimeOffset.UtcNow
        };

        // 최신 epoch만 유지
        while (true)
        {
            if (!_byUid.TryGetValue(uid, out var old))
            {
                if (_byUid.TryAdd(uid, newEntry))
                    return;

                continue;
            }

            // 같은 uid가 이미 있는데,
            // 새 epoch가 더 크면 교체 + old kick
            if (epoch > old.Epoch)
            {
                if (_byUid.TryUpdate(uid, newEntry, old))
                {
                    // old 연결은 즉시 종료
                    old.Conn.Close($"superseded_by_new_epoch:{epoch}");
                    return;
                }
                continue;
            }

            // 새 epoch가 같거나 낮으면 -> 새 연결이 오래된 것
            // 이 경우 새 연결을 종료 (정석적으로 안전)
            conn.Close($"stale_epoch:{epoch} <= {old.Epoch}");
            return;
        }
    }

    /// <summary>
    /// 연결이 끊길 때 호출해서 uid 맵에서 정리.
    /// </summary>
    public void UnbindIfMatch(string uid, string connId, long epoch)
    {
        if (!_byUid.TryGetValue(uid, out var e))
            return;

        if (e.Epoch != epoch)
            return;

        if (!string.Equals(e.Conn.ConnId, connId, StringComparison.Ordinal))
            return;

        _byUid.TryRemove(uid, out _);
    }

    /// <summary>
    /// KickEvent(uid, minEpoch) 처리.
    /// - 현재 연결 epoch가 minEpoch 이상이면 끊는다.
    /// </summary>
    public void KickIfEpochAtLeast(string uid, long minEpoch, string reason)
    {
        if (!_byUid.TryGetValue(uid, out var e))
            return;

        if (e.Epoch >= minEpoch)
        {
            e.Conn.Close(reason);
            _byUid.TryRemove(uid, out _);
        }
    }

    /// <summary>
    /// 서버 내부에서 uid로 현재 연결을 찾고 싶을 때.
    /// </summary>
    public ITcpConnection? TryGet(string uid)
    {
        if (_byUid.TryGetValue(uid, out var e))
            return e.Conn;

        return null;
    }
}
