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
        var selection = WaitingRoomHostSelectionV1Calculator.Calculate(room, nowMs);
        room.PreferredHostUid = selection.PreferredHostUid;
        room.HostCandidateOrder = selection.HostCandidateOrder ?? new List<string>();
        room.HostSelectionMode = selection.Mode;
        room.HostSelectionMetricVersion = selection.MetricVersion;
        room.HostSelectionScore = selection.PreferredHostScore;
        room.HostSelectionUpdatedAtMs = selection.UpdatedAtMs;
        room.HostSelectionCandidates = selection.Candidates ?? new List<WaitingRoomHostSelectionCandidateDto>();

        var orderedMembers = BuildManifestOrder(room);
        string hostUid = ResolveHostUid(room, orderedMembers);
        string hostSteamId64 = room.MemberTransport
            .FirstOrDefault(x => string.Equals(x.Uid, hostUid, StringComparison.OrdinalIgnoreCase))
            ?.SteamId64 ?? "";

        int hostEpoch = Math.Max(1, room.HostEpoch + 1);
        int nextActorId = 1;

        var hostSelectionEntry = room.HostSelectionCandidates
            .FirstOrDefault(x => string.Equals(x.Uid, hostUid, StringComparison.OrdinalIgnoreCase));

        var participants = new List<GameMatchParticipant>(orderedMembers.Count);
        foreach (var uid in orderedMembers)
        {
            var member = room.MemberTransport.FirstOrDefault(x => string.Equals(x.Uid, uid, StringComparison.OrdinalIgnoreCase));
            participants.Add(new GameMatchParticipant
            {
                Uid = uid,
                DisplayName = member?.Name ?? "",
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
            PreferredHostRttMs = hostSelectionEntry?.AveragePairRttMs ?? -1,
            HostSelectionMode = room.HostSelectionMode ?? "",
            HostSelectionMetricVersion = room.HostSelectionMetricVersion ?? WaitingRoomHostSelectionV1Calculator.MetricVersion,
            HostSelectionEpoch = Math.Max(0, room.HostSelectionEpoch),
            HostSelectionScore = room.HostSelectionScore,
            HostSelectionUpdatedAtMs = room.HostSelectionUpdatedAtMs,
            HostCandidateOrder = orderedMembers.ToList(),
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
            "[GameMatch] Stored manifest room={RoomId} match={MatchId} host={HostUid} hostRtt={HostRtt} steamHostRequired={SteamHostRequired} members={MemberCount} order={Order}",
            manifest.RoomId,
            manifest.MatchId,
            manifest.HostUid,
            manifest.PreferredHostRttMs,
            room.UseP2PRelay,
            manifest.Participants.Count,
            string.Join(",", manifest.Participants.Select(x => $"{x.ActorId}:{x.Uid}/{(string.IsNullOrWhiteSpace(x.SteamId64) ? "-" : x.SteamId64)}")));

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

    private static string ResolveHostUid(WaitingRoomDto room, IReadOnlyList<string> orderedMembers)
    {
        if (orderedMembers.Count > 0)
            return orderedMembers[0];

        if (!string.IsNullOrWhiteSpace(room.PreferredHostUid))
            return room.PreferredHostUid;

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

    private static List<string> BuildManifestOrder(WaitingRoomDto room)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (room.HostCandidateOrder != null)
        {
            foreach (var uid in room.HostCandidateOrder)
            {
                if (string.IsNullOrWhiteSpace(uid) || !seen.Add(uid))
                    continue;

                ordered.Add(uid);
            }
        }

        foreach (var uid in room.MemberUids ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(uid) || !seen.Add(uid))
                continue;

            ordered.Add(uid);
        }

        return ordered;
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
    public string HostSelectionMode { get; set; } = "";
    public string HostSelectionMetricVersion { get; set; } = WaitingRoomHostSelectionV1Calculator.MetricVersion;
    public int HostSelectionEpoch { get; set; }
    public float HostSelectionScore { get; set; } = -1f;
    public long HostSelectionUpdatedAtMs { get; set; }
    public List<string> HostCandidateOrder { get; set; } = new();
    public long CreatedAtMs { get; set; }
    public List<GameMatchParticipant> Participants { get; set; } = new();
}

public sealed class GameMatchParticipant
{
    public string Uid { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SteamId64 { get; set; } = "";
    public int ActorId { get; set; }
    public string LoadoutHash { get; set; } = "";
}
