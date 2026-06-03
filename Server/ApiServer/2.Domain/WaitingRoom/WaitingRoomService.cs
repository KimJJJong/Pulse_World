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
    public int HostSelectionEpoch { get; set; }
    public string HostSelectionMode { get; set; } = "";
    public string HostSelectionMetricVersion { get; set; } = WaitingRoomHostSelectionV1Calculator.MetricVersion;
    public float HostSelectionScore { get; set; } = -1f;
    public long HostSelectionUpdatedAtMs { get; set; }
    public List<string> HostCandidateOrder { get; set; } = new();
    public List<WaitingRoomHostSelectionCandidateDto> HostSelectionCandidates { get; set; } = new();
    public string SourceTownRoomId { get; set; } = "";
    public List<string> RequiredMemberUids { get; set; } = new();
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
    public bool SteamEnabled { get; set; }
    public bool SteamInitialized { get; set; }
    public bool SteamLobbyJoined { get; set; }
    public bool SteamReady { get; set; }
    public int CurrentServerRttMs { get; set; } = -1;
    public float CurrentServerLossPct { get; set; }
    public int CurrentServerJitterMs { get; set; } = -1;
    public float AvgFrameMs { get; set; } = -1f;
    public float P95FrameMs { get; set; } = -1f;
    public int SendQueueDepth { get; set; }
    public List<WaitingRoomMeasuredSteamPairDto> MeasuredSteamPairs { get; set; } = new();
    public long HostSelectionReportedAtMs { get; set; }
}

public sealed class WaitingRoomMeasuredSteamPairDto
{
    public string PeerUid { get; set; } = "";
    public string PeerSteamId64 { get; set; } = "";
    public int RttMs { get; set; } = -1;
    public float ConnectionQualityLocal { get; set; } = -1f;
    public float ConnectionQualityRemote { get; set; } = -1f;
    public bool Connected { get; set; }
    public long ReportedAtMs { get; set; }
    public string Source { get; set; } = "";
}

internal sealed class WaitingRoomMemberState
{
    public string Name { get; set; } = "";
    public bool Ready { get; set; }
    public string SteamId64 { get; set; } = "";
    public string ClientVersion { get; set; } = "";
    public int HostProbeRttMs { get; set; } = -1;
    public long HostProbeReportedAtMs { get; set; }
    public bool SteamEnabled { get; set; }
    public bool SteamInitialized { get; set; }
    public bool SteamLobbyJoined { get; set; }
    public bool SteamReady { get; set; }
    public int CurrentServerRttMs { get; set; } = -1;
    public float CurrentServerLossPct { get; set; }
    public int CurrentServerJitterMs { get; set; } = -1;
    public float AvgFrameMs { get; set; } = -1f;
    public float P95FrameMs { get; set; } = -1f;
    public int SendQueueDepth { get; set; }
    public List<WaitingRoomMeasuredSteamPairDto> MeasuredSteamPairs { get; set; } = new();
    public long HostSelectionReportedAtMs { get; set; }
}

public sealed class WaitingRoomService
{
    private const long HostProbeFreshnessWindowMs = 90_000;

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

    public async Task<string?> CreateAsync(
        string title,
        string mapId,
        int maxPlayers,
        string ownerUid,
        string ownerName,
        bool useP2PRelay = false,
        IReadOnlyList<string>? requiredMemberUids = null,
        string sourceTownRoomId = "")
    {
        var roomId = Guid.NewGuid().ToString("N")[..8];
        var key = _redis.KeyWaitingRoom(roomId);

        // Check collision
        if (await _redis.Db.KeyExistsAsync(key)) return null; 

        var requiredMembers = NormalizeRequiredMemberUids(ownerUid, requiredMemberUids);
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
            new("hostSelectionEpoch", 0),
            new("sourceTownRoomId", sourceTownRoomId ?? ""),
            new("requiredMemberUids", JsonSerializer.Serialize(requiredMembers, JsonOptions)),
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
            await BumpHostSelectionEpochAsync(key);
            return (true, "");
        }

        if (count >= max) return (false, "RoomFull");

        var memberState = new WaitingRoomMemberState
        {
            Name = name ?? "",
            Ready = false
        };
        await SaveMemberStateAsync(membersKey, uid, memberState);
        await BumpHostSelectionEpochAsync(key);
        
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
            else
            {
                await BumpHostSelectionEpochAsync(key);
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
        await BumpHostSelectionEpochAsync(key);
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
        await BumpHostSelectionEpochAsync(key);
        return true;
    }

    public async Task<bool> UpdateMemberHostSelectionAsync(
        string roomId,
        string uid,
        string steamId64,
        bool steamEnabled,
        bool steamInitialized,
        bool steamLobbyJoined,
        bool steamReady,
        int currentServerRttMs,
        float currentServerLossPct,
        int currentServerJitterMs,
        float avgFrameMs,
        float p95FrameMs,
        int sendQueueDepth,
        List<WaitingRoomMeasuredSteamPairDto>? measuredSteamPairs,
        long reportedAtMs)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        var membersKey = $"{key}:members";

        if (!await _redis.Db.HashExistsAsync(membersKey, uid))
            return false;

        var state = await GetMemberStateAsync(membersKey, uid);
        state.SteamId64 = steamId64 ?? "";
        state.SteamEnabled = steamEnabled;
        state.SteamInitialized = steamInitialized;
        state.SteamLobbyJoined = steamLobbyJoined;
        state.SteamReady = steamReady;
        state.CurrentServerRttMs = currentServerRttMs;
        state.CurrentServerLossPct = currentServerLossPct >= 0f ? currentServerLossPct : 0f;
        state.CurrentServerJitterMs = currentServerJitterMs;
        state.AvgFrameMs = avgFrameMs;
        state.P95FrameMs = p95FrameMs;
        state.SendQueueDepth = Math.Max(0, sendQueueDepth);
        state.MeasuredSteamPairs = NormalizeMeasuredSteamPairs(measuredSteamPairs);
        state.HostSelectionReportedAtMs = reportedAtMs > 0
            ? reportedAtMs
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await SaveMemberStateAsync(membersKey, uid, state);
        await BumpHostSelectionEpochAsync(key);
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

    public async Task<bool> SetStatusAsync(string roomId, string status)
    {
        var key = _redis.KeyWaitingRoom(roomId);
        if (!await _redis.Db.KeyExistsAsync(key))
            return false;

        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("status", string.IsNullOrWhiteSpace(status) ? "Open" : status.Trim()),
            new("statusUpdatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
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
            HostSelectionEpoch = int.TryParse(dict.GetValueOrDefault("hostSelectionEpoch") ?? "0", out var selectionEpoch) ? selectionEpoch : 0,
            SourceTownRoomId = dict.GetValueOrDefault("sourceTownRoomId") ?? "",
            RequiredMemberUids = DeserializeRequiredMemberUids(dict.GetValueOrDefault("requiredMemberUids") ?? ""),
            MemberUids = new List<string>(),
            MemberReady = new Dictionary<string, bool>(),
            MemberTransport = new List<WaitingRoomMemberTransportDto>(),
            HostCandidateOrder = new List<string>(),
            HostSelectionCandidates = new List<WaitingRoomHostSelectionCandidateDto>()
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
                 HostProbeReportedAtMs = state.HostProbeReportedAtMs,
                 SteamEnabled = state.SteamEnabled,
                 SteamInitialized = state.SteamInitialized,
                 SteamLobbyJoined = state.SteamLobbyJoined,
                 SteamReady = state.SteamReady,
                 CurrentServerRttMs = state.CurrentServerRttMs,
                 CurrentServerLossPct = state.CurrentServerLossPct,
                 CurrentServerJitterMs = state.CurrentServerJitterMs,
                 AvgFrameMs = state.AvgFrameMs,
                 P95FrameMs = state.P95FrameMs,
                 SendQueueDepth = state.SendQueueDepth,
                 MeasuredSteamPairs = CloneMeasuredSteamPairs(state.MeasuredSteamPairs),
                 HostSelectionReportedAtMs = state.HostSelectionReportedAtMs
             });
        }

        var selection = WaitingRoomHostSelectionV1Calculator.Calculate(dto, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        dto.PreferredHostUid = selection.PreferredHostUid;
        dto.HostSelectionMode = selection.Mode;
        dto.HostSelectionMetricVersion = selection.MetricVersion;
        dto.HostSelectionScore = selection.PreferredHostScore;
        dto.HostSelectionUpdatedAtMs = selection.UpdatedAtMs;
        dto.HostCandidateOrder = selection.HostCandidateOrder ?? new List<string>();
        dto.HostSelectionCandidates = selection.Candidates ?? new List<WaitingRoomHostSelectionCandidateDto>();
        
        return (true, dto);
    }

    private static List<string> NormalizeRequiredMemberUids(string ownerUid, IReadOnlyList<string>? requiredMemberUids)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (requiredMemberUids == null || requiredMemberUids.Count <= 0)
            return result;

        if (!string.IsNullOrWhiteSpace(ownerUid) && seen.Add(ownerUid))
            result.Add(ownerUid);

        foreach (var uid in requiredMemberUids)
        {
            if (!string.IsNullOrWhiteSpace(uid) && seen.Add(uid))
                result.Add(uid);
        }

        return result;
    }

    private static List<string> DeserializeRequiredMemberUids(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
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
            if (doc.RootElement.TryGetProperty("SteamEnabled", out var steamEnabled))
                state.SteamEnabled = steamEnabled.GetBoolean();
            if (doc.RootElement.TryGetProperty("SteamInitialized", out var steamInitialized))
                state.SteamInitialized = steamInitialized.GetBoolean();
            if (doc.RootElement.TryGetProperty("SteamLobbyJoined", out var steamLobbyJoined))
                state.SteamLobbyJoined = steamLobbyJoined.GetBoolean();
            if (doc.RootElement.TryGetProperty("SteamReady", out var steamReady))
                state.SteamReady = steamReady.GetBoolean();
            if (doc.RootElement.TryGetProperty("CurrentServerRttMs", out var currentServerRtt) && currentServerRtt.TryGetInt32(out var parsedCurrentServerRtt))
                state.CurrentServerRttMs = parsedCurrentServerRtt;
            if (doc.RootElement.TryGetProperty("CurrentServerLossPct", out var currentServerLoss) && currentServerLoss.TryGetSingle(out var parsedCurrentServerLoss))
                state.CurrentServerLossPct = parsedCurrentServerLoss;
            if (doc.RootElement.TryGetProperty("CurrentServerJitterMs", out var currentServerJitter) && currentServerJitter.TryGetInt32(out var parsedCurrentServerJitter))
                state.CurrentServerJitterMs = parsedCurrentServerJitter;
            if (doc.RootElement.TryGetProperty("AvgFrameMs", out var avgFrame) && avgFrame.TryGetSingle(out var parsedAvgFrame))
                state.AvgFrameMs = parsedAvgFrame;
            if (doc.RootElement.TryGetProperty("P95FrameMs", out var p95Frame) && p95Frame.TryGetSingle(out var parsedP95Frame))
                state.P95FrameMs = parsedP95Frame;
            if (doc.RootElement.TryGetProperty("SendQueueDepth", out var sendQueueDepth) && sendQueueDepth.TryGetInt32(out var parsedSendQueueDepth))
                state.SendQueueDepth = parsedSendQueueDepth;
            if (doc.RootElement.TryGetProperty("MeasuredSteamPairs", out var measuredSteamPairs) && measuredSteamPairs.ValueKind == JsonValueKind.Array)
                state.MeasuredSteamPairs = DeserializeMeasuredSteamPairs(measuredSteamPairs);
            if (doc.RootElement.TryGetProperty("HostSelectionReportedAtMs", out var selectionReportedAt) && selectionReportedAt.TryGetInt64(out var parsedSelectionReportedAt))
                state.HostSelectionReportedAtMs = parsedSelectionReportedAt;
            return state;
        }
        catch
        {
            return new WaitingRoomMemberState();
        }
    }

    private async Task<long> BumpHostSelectionEpochAsync(string roomKey)
    {
        if (string.IsNullOrWhiteSpace(roomKey))
            return 0;

        return await _redis.Db.HashIncrementAsync(roomKey, "hostSelectionEpoch", 1);
    }

    private static List<WaitingRoomMeasuredSteamPairDto> CloneMeasuredSteamPairs(IEnumerable<WaitingRoomMeasuredSteamPairDto>? measuredSteamPairs)
    {
        return NormalizeMeasuredSteamPairs(measuredSteamPairs);
    }

    private static List<WaitingRoomMeasuredSteamPairDto> NormalizeMeasuredSteamPairs(IEnumerable<WaitingRoomMeasuredSteamPairDto>? measuredSteamPairs)
    {
        if (measuredSteamPairs == null)
            return new List<WaitingRoomMeasuredSteamPairDto>();

        return measuredSteamPairs
            .Where(x => x != null
                        && x.Connected
                        && x.RttMs >= 0
                        && (!string.IsNullOrWhiteSpace(x.PeerUid) || !string.IsNullOrWhiteSpace(x.PeerSteamId64)))
            .GroupBy(
                x => !string.IsNullOrWhiteSpace(x.PeerUid) ? $"uid:{x.PeerUid}" : $"steam:{x.PeerSteamId64}",
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.ReportedAtMs)
                .ThenBy(x => x.RttMs)
                .First())
            .Select(x => new WaitingRoomMeasuredSteamPairDto
            {
                PeerUid = x.PeerUid ?? "",
                PeerSteamId64 = x.PeerSteamId64 ?? "",
                RttMs = x.RttMs,
                ConnectionQualityLocal = x.ConnectionQualityLocal,
                ConnectionQualityRemote = x.ConnectionQualityRemote,
                Connected = x.Connected,
                ReportedAtMs = x.ReportedAtMs,
                Source = x.Source ?? ""
            })
            .OrderBy(x => string.IsNullOrWhiteSpace(x.PeerUid) ? x.PeerSteamId64 : x.PeerUid, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<WaitingRoomMeasuredSteamPairDto> DeserializeMeasuredSteamPairs(JsonElement element)
    {
        var result = new List<WaitingRoomMeasuredSteamPairDto>();
        foreach (var item in element.EnumerateArray())
        {
            string peerUid = item.TryGetProperty("PeerUid", out var peerUidPascal)
                ? peerUidPascal.GetString() ?? ""
                : (item.TryGetProperty("peerUid", out var peerUidCamel) ? peerUidCamel.GetString() ?? "" : "");
            string peerSteamId64 = item.TryGetProperty("PeerSteamId64", out var peerSteamPascal)
                ? peerSteamPascal.GetString() ?? ""
                : (item.TryGetProperty("peerSteamId64", out var peerSteamCamel) ? peerSteamCamel.GetString() ?? "" : "");
            int rttMs = item.TryGetProperty("RttMs", out var rttPascal) && rttPascal.TryGetInt32(out var parsedRttPascal)
                ? parsedRttPascal
                : (item.TryGetProperty("rttMs", out var rttCamel) && rttCamel.TryGetInt32(out var parsedRttCamel) ? parsedRttCamel : -1);
            float qualityLocal = item.TryGetProperty("ConnectionQualityLocal", out var qualityLocalPascal) && qualityLocalPascal.TryGetSingle(out var parsedQualityLocalPascal)
                ? parsedQualityLocalPascal
                : (item.TryGetProperty("connectionQualityLocal", out var qualityLocalCamel) && qualityLocalCamel.TryGetSingle(out var parsedQualityLocalCamel) ? parsedQualityLocalCamel : -1f);
            float qualityRemote = item.TryGetProperty("ConnectionQualityRemote", out var qualityRemotePascal) && qualityRemotePascal.TryGetSingle(out var parsedQualityRemotePascal)
                ? parsedQualityRemotePascal
                : (item.TryGetProperty("connectionQualityRemote", out var qualityRemoteCamel) && qualityRemoteCamel.TryGetSingle(out var parsedQualityRemoteCamel) ? parsedQualityRemoteCamel : -1f);
            bool connected = item.TryGetProperty("Connected", out var connectedPascal)
                ? connectedPascal.GetBoolean()
                : (item.TryGetProperty("connected", out var connectedCamel) && connectedCamel.GetBoolean());
            long reportedAtMs = item.TryGetProperty("ReportedAtMs", out var reportedAtPascal) && reportedAtPascal.TryGetInt64(out var parsedReportedAtPascal)
                ? parsedReportedAtPascal
                : (item.TryGetProperty("reportedAtMs", out var reportedAtCamel) && reportedAtCamel.TryGetInt64(out var parsedReportedAtCamel) ? parsedReportedAtCamel : 0L);
            string source = item.TryGetProperty("Source", out var sourcePascal)
                ? sourcePascal.GetString() ?? ""
                : (item.TryGetProperty("source", out var sourceCamel) ? sourceCamel.GetString() ?? "" : "");

            result.Add(new WaitingRoomMeasuredSteamPairDto
            {
                PeerUid = peerUid,
                PeerSteamId64 = peerSteamId64,
                RttMs = rttMs,
                ConnectionQualityLocal = qualityLocal,
                ConnectionQualityRemote = qualityRemote,
                Connected = connected,
                ReportedAtMs = reportedAtMs,
                Source = source
            });
        }

        return NormalizeMeasuredSteamPairs(result);
    }
}
