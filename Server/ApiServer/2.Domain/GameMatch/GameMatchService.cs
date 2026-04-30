using System.Security.Cryptography;
using System.Text.Json;
using ApiServer.Domain.WaitingRoom;
using ApiServer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApiServer.Domain.GameMatch;

public sealed class GameMatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private readonly RedisStore _redis;
    private readonly ILogger<GameMatchService> _logger;

    public GameMatchService(RedisStore redis, ILogger<GameMatchService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<GameMatchManifest> CreateOrReplaceForWaitingRoomAsync(
        WaitingRoomDto room,
        string networkMode,
        string protocolVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var orderedMembers = OrderHostCandidates(room);
        string hostUid = orderedMembers.FirstOrDefault() ?? SelectPreferredHostUid(room);
        string hostSteamId64 = room.MemberTransport
            .FirstOrDefault(x => string.Equals(x.Uid, hostUid, StringComparison.OrdinalIgnoreCase))
            ?.SteamId64 ?? "";

        int hostEpoch = Math.Max(1, room.HostEpoch + 1);
        int nextActorId = 1;

        var hostEntry = room.MemberTransport
            .FirstOrDefault(x => string.Equals(x.Uid, hostUid, StringComparison.OrdinalIgnoreCase));

        var participants = new List<GameMatchParticipant>(orderedMembers.Count);
        foreach (var uid in orderedMembers)
        {
            var member = room.MemberTransport.FirstOrDefault(x => string.Equals(x.Uid, uid, StringComparison.OrdinalIgnoreCase));
            participants.Add(new GameMatchParticipant
            {
                Uid = uid,
                SteamId64 = member?.SteamId64 ?? "",
                ActorId = nextActorId++,
                LoadoutHash = ""
            });
        }

        var manifest = new GameMatchManifest
        {
            MatchId = $"{room.RoomId}:{nowMs}",
            RoomId = room.RoomId,
            NetworkMode = string.IsNullOrWhiteSpace(networkMode) ? "steam_p2p_host" : networkMode,
            ProtocolVersion = string.IsNullOrWhiteSpace(protocolVersion) ? "0.0.0" : protocolVersion,
            MapId = room.MapId ?? "",
            StageSeed = RandomNumberGenerator.GetInt32(1, int.MaxValue),
            SongStartDelayMs = 3000,
            HostUid = hostUid,
            HostSteamId64 = hostSteamId64,
            HostEpoch = hostEpoch,
            PreferredHostRttMs = hostEntry?.HostProbeRttMs ?? -1,
            CreatedAtMs = nowMs,
            Participants = participants
        };

        string json = JsonSerializer.Serialize(manifest, JsonOptions);
        await _redis.Db.HashSetAsync(_redis.KeyWaitingRoom(room.RoomId), new HashEntry[]
        {
            new("preferredHostUid", manifest.HostUid),
            new("hostEpoch", manifest.HostEpoch),
        });
        await _redis.Db.StringSetAsync(_redis.KeyGameMatchManifest(room.RoomId), json, expiry: TimeSpan.FromHours(1));
        await _redis.Db.StringSetAsync(_redis.KeyGameMatchManifestByMatchId(manifest.MatchId), json, expiry: TimeSpan.FromHours(1));

        _logger.LogInformation(
            "[GameMatch] Stored manifest room={RoomId} match={MatchId} host={HostUid} hostRtt={HostRtt} members={MemberCount}",
            manifest.RoomId,
            manifest.MatchId,
            manifest.HostUid,
            manifest.PreferredHostRttMs,
            manifest.Participants.Count);

        return manifest;
    }

    public async Task<GameMatchManifest?> GetByRoomIdAsync(string roomId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return null;

        return await ReadManifestAsync(_redis.KeyGameMatchManifest(roomId), ct);
    }

    public async Task<GameMatchManifest?> GetByMatchIdAsync(string matchId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            return null;

        return await ReadManifestAsync(_redis.KeyGameMatchManifestByMatchId(matchId), ct);
    }

    private static string SelectPreferredHostUid(WaitingRoomDto room)
    {
        var orderedMembers = OrderHostCandidates(room);
        if (orderedMembers.Count > 0)
            return orderedMembers[0];

        if (!string.IsNullOrWhiteSpace(room.OwnerUid))
            return room.OwnerUid;

        return room.MemberUids.FirstOrDefault() ?? "";
    }

    private async Task<GameMatchManifest?> ReadManifestAsync(string redisKey, CancellationToken ct)
    {
        try
        {
            var json = await _redis.Db.StringGetAsync(redisKey);
            if (json.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<GameMatchManifest>(json!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GameMatch] Failed to read manifest key={RedisKey}", redisKey);
            return null;
        }
    }

    private static List<string> OrderHostCandidates(WaitingRoomDto room)
    {
        var transportByUid = (room.MemberTransport ?? new List<WaitingRoomMemberTransportDto>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Uid))
            .GroupBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(x => x.HostProbeRttMs >= 0 && x.HostProbeReportedAtMs > 0 ? 0 : 1)
                    .ThenBy(x => x.HostProbeRttMs >= 0 ? x.HostProbeRttMs : int.MaxValue)
                    .ThenByDescending(x => x.HostProbeReportedAtMs)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        return room.MemberUids
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(uid =>
            {
                return transportByUid.TryGetValue(uid, out var state)
                       && state.HostProbeRttMs >= 0
                       && state.HostProbeReportedAtMs > 0
                    ? 0
                    : 1;
            })
            .ThenBy(uid =>
            {
                return transportByUid.TryGetValue(uid, out var state)
                       && state.HostProbeRttMs >= 0
                    ? state.HostProbeRttMs
                    : int.MaxValue;
            })
            .ThenBy(uid => string.Equals(uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(uid => uid, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class GameMatchManifest
{
    public string MatchId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string NetworkMode { get; set; } = "";
    public string ProtocolVersion { get; set; } = "";
    public string MapId { get; set; } = "";
    public int StageSeed { get; set; }
    public int SongStartDelayMs { get; set; }
    public string HostUid { get; set; } = "";
    public string HostSteamId64 { get; set; } = "";
    public int HostEpoch { get; set; }
    public int PreferredHostRttMs { get; set; } = -1;
    public long CreatedAtMs { get; set; }
    public List<GameMatchParticipant> Participants { get; set; } = new();
}

public sealed class GameMatchParticipant
{
    public string Uid { get; set; } = "";
    public string SteamId64 { get; set; } = "";
    public int ActorId { get; set; }
    public string LoadoutHash { get; set; } = "";
}
