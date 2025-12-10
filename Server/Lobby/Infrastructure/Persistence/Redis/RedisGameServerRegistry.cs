using StackExchange.Redis;
using System.Globalization;

namespace Lobby.Infrastructure.GameServers;

using Lobby.Domain.GameServer;

public sealed class RedisGameServerRegistry : IGameServerRegistry
{
    private readonly IDatabase _db;
    private readonly string _indexKey;
    private readonly string _prefix;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <param name="db">mux.GetDatabase()</param>
    /// <param name="prefix">base prefix (default "gs:")</param>
    /// <param name="indexKey">index set key (default "gs:index")</param>
    public RedisGameServerRegistry(IDatabase db, string prefix = "gs:", string indexKey = "gs:index")
    {
        _db = db;
        _prefix = prefix;
        _indexKey = indexKey;
    }

    private string Key(string gsId) => $"{_prefix}{gsId}";

    public async Task RegisterOrUpdateAsync(GameServerInfo info, CancellationToken ct = default)
    {
        if (info is null) throw new ArgumentNullException(nameof(info));
        if (string.IsNullOrWhiteSpace(info.Id))
            throw new ArgumentException("GameServerInfo.Id is required.", nameof(info));

        var key = Key(info.Id);

        // Hash save Value
        var entries = new HashEntry[]
        {
            new("id", info.Id),
            new("host", info.Host),
            new("port", info.Port),
            new("maxRooms", info.MaxRooms),
            new("currentRooms", info.CurrentRooms),
            new("status", (int)info.Status),
            // Unix ms 
            new("lastHeartbeatMs", info.LastHeartbeat.ToUnixTimeMilliseconds())
        };

        // index set에 id 추가 + hash 업데이트
        // StackExchange.Redis는 CancellationToken을 직접 받지 않으므로 무시
        _ = await _db.SetAddAsync(_indexKey, info.Id).ConfigureAwait(false);
        await _db.HashSetAsync(key, entries).ConfigureAwait(false);
    }

    public async Task<GameServerInfo?> GetAsync(string gsId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gsId))
            throw new ArgumentException("gsId is required.", nameof(gsId));

        var key = Key(gsId);
        var entries = await _db.HashGetAllAsync(key).ConfigureAwait(false);

        if (entries.Length == 0)
            return null;

        return Map(entries);
    }

    public async Task<IReadOnlyList<GameServerInfo>> GetAllAsync(CancellationToken ct = default)
    {
        var ids = await _db.SetMembersAsync(_indexKey).ConfigureAwait(false);
        if (ids.Length == 0)
            return Array.Empty<GameServerInfo>();

        var tasks = new List<Task<HashEntry[]>>(ids.Length);

        foreach (var id in ids)
        {
            var key = Key(id!);
            tasks.Add(_db.HashGetAllAsync(key));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var list = new List<GameServerInfo>(ids.Length);
        foreach (var task in tasks)
        {
            var entries = task.Result;
            if (entries.Length == 0)
                continue;

            var info = Map(entries);
            if (info != null)
                list.Add(info);
        }

        return list;
    }

    public async Task RemoveAsync(string gsId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gsId))
            return;

        var key = Key(gsId);
        _ = await _db.KeyDeleteAsync(key).ConfigureAwait(false);
        _ = await _db.SetRemoveAsync(_indexKey, gsId).ConfigureAwait(false);
    }

    private static GameServerInfo? Map(HashEntry[] entries)
    {
        // HashEntry[] -> GameServerInfo 변환
        // 없는 필드에 대한 기본값 처리
        string? GetStr(string name)
        {
            var v = entries.FirstOrDefault(e => e.Name == name).Value;
            // RedisValue -> string? 변환
            return v.IsNull ? null : (string)v!;
        }

        int GetInt(string name, int defaultValue = 0)
        {
            var s = GetStr(name);
            if (string.IsNullOrEmpty(s))
                return defaultValue;

            return int.TryParse(s, NumberStyles.Integer, Invariant, out var i)
                ? i
                : defaultValue;
        }

        long GetLong(string name, long defaultValue = 0)
        {
            var s = GetStr(name);
            if (string.IsNullOrEmpty(s))
                return defaultValue;

            return long.TryParse(s, NumberStyles.Integer, Invariant, out var l)
                ? l
                : defaultValue;
        }

        var id = GetStr("id");
        if (string.IsNullOrEmpty(id))
            return null;

        var host = GetStr("host");
        if (string.IsNullOrEmpty(host))
            host = "localhost";

        var port = GetInt("port");
        var maxRooms = GetInt("maxRooms", 0);
        var curRooms = GetInt("currentRooms", 0);
        var statusInt = GetInt("status", (int)GameServerStatus.Unknown);
        var lastMs = GetLong("lastHeartbeatMs", 0);

        var status = Enum.IsDefined(typeof(GameServerStatus), statusInt)
            ? (GameServerStatus)statusInt
            : GameServerStatus.Unknown;

        var lastHeartbeat = lastMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastMs)
            : DateTimeOffset.MinValue;

        return new GameServerInfo
        {
            Id = id!,
            Host = host!,
            Port = port,
            MaxRooms = maxRooms,
            CurrentRooms = curRooms,
            Status = status,
            LastHeartbeat = lastHeartbeat
        };
    }

}
