using ApiServer.Infrastructure.Persistence;
using StackExchange.Redis;
using System.Text.Json;

namespace ApiServer.Domain.WaitingRoom;

public class WaitingRoomDto
{
    public string RoomId { get; set; } = "";
    public string Title { get; set; } = "";
    public string MapId { get; set; } = "";
    public int MaxPlayers { get; set; }
    public string OwnerUid { get; set; } = "";
    public string Status { get; set; } = "";
    public bool UseP2PRelay { get; set; }
    public string SteamLobbyId { get; set; } = "";
    public string PreferredHostUid { get; set; } = "";
    public int HostEpoch { get; set; }
    public List<string> MemberUids { get; set; } = new();
    public Dictionary<string, bool> MemberReady { get; set; } = new();
    public List<WaitingRoomMemberTransportDto> MemberTransport { get; set; } = new();
}

public sealed class WaitingRoomMemberTransportDto
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string SteamId64 { get; set; } = "";
    public string ClientVersion { get; set; } = "";
    public int HostProbeRttMs { get; set; } = -1;
    public long HostProbeReportedAtMs { get; set; }
}

internal sealed class WaitingRoomMemberState
{
    public string Name { get; set; } = "";
    public bool Ready { get; set; }
    public string SteamId64 { get; set; } = "";
    public string ClientVersion { get; set; } = "";
    public int HostProbeRttMs { get; set; } = -1;
    public long HostProbeReportedAtMs { get; set; }
}

public sealed class WaitingRoomService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private readonly RedisStore _redis;

    public WaitingRoomService(RedisStore redis)
    {
        _redis = redis;
    }

    public async Task<string?> CreateAsync(string title, string mapId, int maxPlayers, string ownerUid, string ownerName, bool useP2PRelay = false)
    {
        var roomId = Guid.NewGuid().ToString("N")[..8];
        var key = _redis.KeyWaitingRoom(roomId);

        // Check collision
        if (await _redis.Db.KeyExistsAsync(key)) return null; 

        var entries = new HashEntry[]
        {
            new("title", title),
            new("mapId", mapId),
            new("maxPlayers", maxPlayers),
            new("ownerUid", ownerUid),
            new("status", "Open"),
            new("useP2PRelay", useP2PRelay ? 1 : 0),
            new("steamLobbyId", ""),
            new("preferredHostUid", ownerUid),
            new("hostEpoch", 0),
            new("createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        };

        await _redis.Db.HashSetAsync(key, entries);
        await _redis.Db.KeyExpireAsync(key, TimeSpan.FromHours(1)); 

        // Add index
        await _redis.Db.SetAddAsync(_redis.KeyWaitingRoomIndex(), roomId);

        // Add owner
        await JoinAsync(roomId, ownerUid, ownerName);
        await UpdateMemberTransportAsync(roomId, ownerUid, ownerName, "", "", -1, 0);
        
        return roomId;
    }

    public async Task<(bool, string error)> JoinAsync(string roomId, string uid, string name)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        if (!await _redis.Db.KeyExistsAsync(key)) return (false, "RoomNotFound");

        var membersKey = $"{key}:members";
        var count = await _redis.Db.HashLengthAsync(membersKey);
        var maxVal = await _redis.Db.HashGetAsync(key, "maxPlayers");
        int max = (int)maxVal;

        // If already member, keep transport state but refresh name.
        if (await _redis.Db.HashExistsAsync(membersKey, uid))
        {
            var state = await GetMemberStateAsync(membersKey, uid);
            state.Name = name ?? "";
            await SaveMemberStateAsync(membersKey, uid, state);
            return (true, "");
        }

        if (count >= max) return (false, "RoomFull");

        var memberState = new WaitingRoomMemberState
        {
            Name = name ?? "",
            Ready = false
        };
        await SaveMemberStateAsync(membersKey, uid, memberState);
        
        return (true, "");
    }

    public async Task<bool> LeaveAsync(string roomId, string uid)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        var membersKey = $"{key}:members";
        
        var removed = await _redis.Db.HashDeleteAsync(membersKey, uid);
        
        var count = await _redis.Db.HashLengthAsync(membersKey);
        if (count == 0)
        {
            await _redis.Db.KeyDeleteAsync(key);
            await _redis.Db.KeyDeleteAsync(membersKey);
            await _redis.Db.SetRemoveAsync(_redis.KeyWaitingRoomIndex(), roomId);
        }
        else
        {
            // If owner left, handle owner migration? For now, simplistic: 
            // If owner leaves, room might remain "leaderless" or pick first.
            // Requirement says "Room deleted" or "Host starts".
            // If host leaves, maybe delete room?
            var owner = (string?)await _redis.Db.HashGetAsync(key, "ownerUid");
            if (owner == uid)
            {
               // Host Left -> Close Room
               await _redis.Db.KeyDeleteAsync(key);
               await _redis.Db.KeyDeleteAsync(membersKey);
               await _redis.Db.SetRemoveAsync(_redis.KeyWaitingRoomIndex(), roomId);
            }
        }
        
        return removed;
    }

    public async Task<bool> SetReadyAsync(string roomId, string uid, bool ready)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        var membersKey = $"{key}:members";

        if (!await _redis.Db.HashExistsAsync(membersKey, uid))
            return false;

        var state = await GetMemberStateAsync(membersKey, uid);
        state.Ready = ready;
        await SaveMemberStateAsync(membersKey, uid, state);
        return true;
    }

    public async Task<bool> UpdateMemberTransportAsync(
        string roomId,
        string uid,
        string name,
        string clientVersion,
        string steamId64,
        int hostProbeRttMs,
        long hostProbeReportedAtMs)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        var membersKey = $"{key}:members";

        if (!await _redis.Db.HashExistsAsync(membersKey, uid))
            return false;

        var state = await GetMemberStateAsync(membersKey, uid);
        if (!string.IsNullOrWhiteSpace(name))
            state.Name = name;
        if (!string.IsNullOrWhiteSpace(clientVersion))
            state.ClientVersion = clientVersion;
        if (!string.IsNullOrWhiteSpace(steamId64))
            state.SteamId64 = steamId64;
        if (hostProbeRttMs >= 0)
            state.HostProbeRttMs = hostProbeRttMs;
        if (hostProbeReportedAtMs > 0)
            state.HostProbeReportedAtMs = hostProbeReportedAtMs;

        await SaveMemberStateAsync(membersKey, uid, state);
        return true;
    }

    public async Task<bool> BindSteamLobbyAsync(string roomId, string steamLobbyId)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        if (!await _redis.Db.KeyExistsAsync(key))
            return false;

        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("steamLobbyId", steamLobbyId ?? ""),
        });
        return true;
    }

    public async Task<(bool exists, WaitingRoomDto? dto)> GetAsync(string roomId)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        if (!await _redis.Db.KeyExistsAsync(key)) return (false, null);

        var meta = await _redis.Db.HashGetAllAsync(key);
        var dict = meta.ToDictionary(k => k.Name.ToString(), v => v.Value.ToString());

        var membersKey = $"{key}:members";
        var members = await _redis.Db.HashGetAllAsync(membersKey);

        var dto = new WaitingRoomDto 
        {
            RoomId = roomId,
            Title = dict.GetValueOrDefault("title") ?? "",
            MapId = dict.GetValueOrDefault("mapId") ?? "",
            MaxPlayers = int.Parse(dict.GetValueOrDefault("maxPlayers") ?? "0"),
            OwnerUid = dict.GetValueOrDefault("ownerUid") ?? "",
            Status = dict.GetValueOrDefault("status") ?? "",
            UseP2PRelay = int.TryParse(dict.GetValueOrDefault("useP2PRelay") ?? "0", out var relayVal) && relayVal != 0,
            SteamLobbyId = dict.GetValueOrDefault("steamLobbyId") ?? "",
            PreferredHostUid = dict.GetValueOrDefault("preferredHostUid") ?? "",
            HostEpoch = int.TryParse(dict.GetValueOrDefault("hostEpoch") ?? "0", out var hostEpoch) ? hostEpoch : 0,
            MemberUids = new List<string>(),
            MemberReady = new Dictionary<string, bool>(),
            MemberTransport = new List<WaitingRoomMemberTransportDto>()
        };

        foreach(var m in members)
        {
             var uid = m.Name.ToString();
             dto.MemberUids.Add(uid);
             var state = DeserializeMemberState(m.Value);
             dto.MemberReady[uid] = state.Ready;
             dto.MemberTransport.Add(new WaitingRoomMemberTransportDto
             {
                 Uid = uid,
                 Name = state.Name,
                 SteamId64 = state.SteamId64,
                 ClientVersion = state.ClientVersion,
                 HostProbeRttMs = state.HostProbeRttMs,
                 HostProbeReportedAtMs = state.HostProbeReportedAtMs
             });
        }

        dto.PreferredHostUid = SelectPreferredHostUid(dto);
        
        return (true, dto);
    }

    public async Task<(List<WaitingRoomDto> rooms, string nextCursor)> GetListAsync(int limit, string cursor)
    {
        if (limit <= 0) limit = 10;
        if (string.IsNullOrEmpty(cursor)) cursor = "0";

        var result = await _redis.Db.ExecuteAsync("SSCAN", _redis.KeyWaitingRoomIndex(), cursor, "COUNT", limit);
        var resArr = (RedisResult[]?)result ?? Array.Empty<RedisResult>();
        if (resArr.Length < 2)
            return (new List<WaitingRoomDto>(), "0");

        var nextCursor = resArr[0].ToString() ?? "0";
        var ids = (RedisResult[]?)resArr[1] ?? Array.Empty<RedisResult>();

        var tasks = new List<Task<(bool, WaitingRoomDto?)>>();
        foreach (var id in ids)
        {
            var roomId = id.ToString();
            if (!string.IsNullOrWhiteSpace(roomId))
                tasks.Add(GetAsync(roomId));
        }

        var results = await Task.WhenAll(tasks);
        var list = new List<WaitingRoomDto>();
        foreach (var (ok, dto) in results)
        {
            if (ok && dto != null) list.Add(dto);
        }

        return (list, nextCursor);
    }

    private static string SelectPreferredHostUid(WaitingRoomDto room)
    {
        var best = room.MemberTransport
            .Where(x => !string.IsNullOrWhiteSpace(x.Uid))
            .Where(x => x.HostProbeRttMs >= 0)
            .Where(x => x.HostProbeReportedAtMs > 0)
            .OrderBy(x => x.HostProbeRttMs)
            .ThenBy(x => string.Equals(x.Uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (best != null)
            return best.Uid;

        if (!string.IsNullOrWhiteSpace(room.OwnerUid))
            return room.OwnerUid;

        return room.MemberUids.FirstOrDefault() ?? "";
    }

    private async Task<WaitingRoomMemberState> GetMemberStateAsync(string membersKey, string uid)
    {
        var val = await _redis.Db.HashGetAsync(membersKey, uid);
        return DeserializeMemberState(val);
    }

    private async Task SaveMemberStateAsync(string membersKey, string uid, WaitingRoomMemberState state)
    {
        var memberData = JsonSerializer.Serialize(state, JsonOptions);
        await _redis.Db.HashSetAsync(membersKey, uid, memberData);
    }

    private static WaitingRoomMemberState DeserializeMemberState(RedisValue value)
    {
        if (!value.HasValue)
            return new WaitingRoomMemberState();

        try
        {
            var parsed = JsonSerializer.Deserialize<WaitingRoomMemberState>(value.ToString(), JsonOptions);
            if (parsed != null)
                return parsed;
        }
        catch
        {
            // Backward compatibility with older payload shape.
        }

        try
        {
            using var doc = JsonDocument.Parse(value.ToString());
            var state = new WaitingRoomMemberState();
            if (doc.RootElement.TryGetProperty("Name", out var name))
                state.Name = name.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("Ready", out var ready))
                state.Ready = ready.GetBoolean();
            if (doc.RootElement.TryGetProperty("SteamId64", out var steam))
                state.SteamId64 = steam.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("ClientVersion", out var version))
                state.ClientVersion = version.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("HostProbeRttMs", out var rtt) && rtt.TryGetInt32(out var parsedRtt))
                state.HostProbeRttMs = parsedRtt;
            if (doc.RootElement.TryGetProperty("HostProbeReportedAtMs", out var reported) && reported.TryGetInt64(out var parsedReported))
                state.HostProbeReportedAtMs = parsedReported;
            return state;
        }
        catch
        {
            return new WaitingRoomMemberState();
        }
    }
}
