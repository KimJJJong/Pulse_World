using System.Text.Json;
using ApiServer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApiServer.Domain.Town;

public sealed class TownRoomDto
{
    public string RoomId { get; set; } = "";
    public string Title { get; set; } = "";
    public string MapId { get; set; } = "";
    public int MaxPlayers { get; set; }
    public string OwnerUid { get; set; } = "";
    public string HostUid { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsPublic { get; set; } = true;
    public string SteamLobbyId { get; set; } = "";
    public string ActiveGameRoomId { get; set; } = "";
    public string ActiveGameMapId { get; set; } = "";
    public string ActiveGameTitle { get; set; } = "";
    public string ActiveGameHostUid { get; set; } = "";
    public long ActiveGameCreatedAtMs { get; set; }
    public long CreatedAtMs { get; set; }
    public List<TownRoomParticipantDto> Participants { get; set; } = new();
}

public sealed class TownRoomParticipantDto
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string SteamId64 { get; set; } = "";
    public string ClientVersion { get; set; } = "";
    public long JoinedAtMs { get; set; }
}

public sealed class TownMatchManifest
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
    public string HostSelectionMode { get; set; } = "owner_locked";
    public string HostSelectionMetricVersion { get; set; } = "town-owner-host-v1";
    public int HostSelectionEpoch { get; set; }
    public float HostSelectionScore { get; set; } = 1f;
    public long HostSelectionUpdatedAtMs { get; set; }
    public List<string> HostCandidateOrder { get; set; } = new();
    public long CreatedAtMs { get; set; }
    public List<TownMatchParticipant> Participants { get; set; } = new();
}

public sealed class TownMatchParticipant
{
    public string Uid { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SteamId64 { get; set; } = "";
    public int ActorId { get; set; }
    public string LoadoutHash { get; set; } = "";
}

public sealed class TownRoomService
{
    private const int FirstTownActorId = 10;
    private const int DefaultMaxPlayers = 16;
    private static readonly TimeSpan RoomTtl = TimeSpan.FromHours(4);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private readonly RedisStore _redis;
    private readonly ILogger<TownRoomService> _logger;

    public TownRoomService(RedisStore redis, ILogger<TownRoomService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<TownRoomDto?> CreateAsync(
        string title,
        string mapId,
        int maxPlayers,
        string ownerUid,
        string ownerName,
        string ownerSteamId64,
        string clientVersion,
        bool isPublic = true)
    {
        if (string.IsNullOrWhiteSpace(ownerUid) || string.IsNullOrWhiteSpace(mapId))
            return null;

        var roomId = Guid.NewGuid().ToString("N")[..8];
        var key = _redis.KeyTownRoom(roomId);
        if (await _redis.Db.KeyExistsAsync(key))
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=room_create_fail reason=id_collision room={RoomId} map={MapId} owner={OwnerUid}",
                roomId,
                mapId,
                ownerUid);
            return null;
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var clampedMax = Math.Clamp(maxPlayers <= 0 ? DefaultMaxPlayers : maxPlayers, 2, 64);
        _logger.LogInformation(
            "[TownRoomLifecycle] event=room_create_request room={RoomId} map={MapId} owner={OwnerUid} max={MaxPlayers}",
            roomId,
            mapId,
            ownerUid,
            clampedMax);

        await _redis.Db.HashSetAsync(key, new HashEntry[]
        {
            new("title", string.IsNullOrWhiteSpace(title) ? $"{ownerName}'s Town" : title),
            new("mapId", mapId),
            new("maxPlayers", clampedMax),
            new("ownerUid", ownerUid),
            new("hostUid", ownerUid),
            new("status", "Open"),
            new("isPublic", isPublic ? "1" : "0"),
            new("steamLobbyId", ""),
            new("activeGameRoomId", ""),
            new("activeGameMapId", ""),
            new("activeGameTitle", ""),
            new("activeGameHostUid", ""),
            new("activeGameCreatedAt", 0),
            new("createdAt", nowMs)
        });
        await _redis.Db.KeyExpireAsync(key, RoomTtl);
        await _redis.Db.SetAddAsync(_redis.KeyTownRoomIndex(), roomId);

        var joined = await JoinAsync(roomId, ownerUid, ownerName, ownerSteamId64, clientVersion);
        if (!joined.ok)
        {
            await DeleteAsync(roomId, $"owner_join_failed:{joined.error}");
            _logger.LogWarning(
                "[TownRoomLifecycle] event=room_create_fail reason=owner_join_failed error={Error} room={RoomId} map={MapId} owner={OwnerUid}",
                joined.error,
                roomId,
                mapId,
                ownerUid);
            return null;
        }

        await CloseActiveRoomsForOwnerAsync(ownerUid, roomId);

        _logger.LogInformation(
            "[TownRoomLifecycle] event=room_create room={RoomId} map={MapId} owner={OwnerUid} host={HostUid} max={MaxPlayers}",
            roomId,
            mapId,
            ownerUid,
            ownerUid,
            clampedMax);
        _logger.LogInformation("[TownRoom] Created room={RoomId} map={MapId} owner={OwnerUid}", roomId, mapId, ownerUid);
        return await GetAsync(roomId);
    }

    public async Task<(bool ok, string error)> JoinAsync(
        string roomId,
        string uid,
        string name,
        string steamId64,
        string clientVersion)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return (false, "RoomIdRequired");
        if (string.IsNullOrWhiteSpace(uid))
            return (false, "UidRequired");

        var key = _redis.KeyTownRoom(roomId);
        if (!await _redis.Db.KeyExistsAsync(key))
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=participant_join_fail reason=room_not_found room={RoomId} uid={Uid}",
                roomId,
                uid);
            return (false, "RoomNotFound");
        }

        var status = (string?)await _redis.Db.HashGetAsync(key, "status") ?? "";
        if (!string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=participant_join_fail reason=room_closed room={RoomId} uid={Uid} status={Status}",
                roomId,
                uid,
                status);
            return (false, "RoomClosed");
        }

        var membersKey = MembersKey(key);
        if (!await _redis.Db.HashExistsAsync(membersKey, uid))
        {
            var count = await _redis.Db.HashLengthAsync(membersKey);
            var maxVal = await _redis.Db.HashGetAsync(key, "maxPlayers");
            var max = maxVal.HasValue ? (int)maxVal : DefaultMaxPlayers;
            if (count >= max)
            {
                _logger.LogWarning(
                    "[TownRoomLifecycle] event=participant_join_fail reason=room_full room={RoomId} uid={Uid} participants={Participants}/{MaxPlayers}",
                    roomId,
                    uid,
                    count,
                    max);
                return (false, "RoomFull");
            }
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var participant = await GetParticipantAsync(membersKey, uid) ?? new TownRoomParticipantDto
        {
            Uid = uid,
            JoinedAtMs = nowMs
        };

        participant.Name = string.IsNullOrWhiteSpace(name) ? participant.Name : name;
        participant.SteamId64 = string.IsNullOrWhiteSpace(steamId64) ? participant.SteamId64 : steamId64;
        participant.ClientVersion = string.IsNullOrWhiteSpace(clientVersion) ? participant.ClientVersion : clientVersion;
        if (participant.JoinedAtMs <= 0)
            participant.JoinedAtMs = nowMs;

        await _redis.Db.HashSetAsync(membersKey, uid, JsonSerializer.Serialize(participant, JsonOptions));
        await _redis.Db.KeyExpireAsync(key, RoomTtl);
        await _redis.Db.KeyExpireAsync(membersKey, RoomTtl);
        var afterCount = await _redis.Db.HashLengthAsync(membersKey);
        _logger.LogInformation(
            "[TownRoomLifecycle] event=participant_join room={RoomId} uid={Uid} steam={SteamId64} client={ClientVersion} participants={Participants}",
            roomId,
            uid,
            participant.SteamId64,
            participant.ClientVersion,
            afterCount);
        return (true, "");
    }

    public async Task<bool> LeaveAsync(string roomId, string uid, string reason = "client_leave")
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(uid))
            return false;

        reason = string.IsNullOrWhiteSpace(reason) ? "client_leave" : reason.Trim();
        var room = await GetAsync(roomId);
        if (room == null)
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=participant_leave_skip reason=room_not_found leaveReason={LeaveReason} room={RoomId} uid={Uid}",
                reason,
                roomId,
                uid);
            return false;
        }

        _logger.LogInformation(
            "[TownRoomLifecycle] event=participant_leave_request reason={Reason} room={RoomId} map={MapId} uid={Uid} owner={OwnerUid} participants={Participants}",
            reason,
            roomId,
            room.MapId,
            uid,
            room.OwnerUid,
            room.Participants.Count);

        if (string.Equals(room.OwnerUid, uid, StringComparison.OrdinalIgnoreCase))
        {
            if (IsImplicitDisconnectReason(reason) && !string.IsNullOrWhiteSpace(room.ActiveGameRoomId))
            {
                _logger.LogInformation(
                    "[TownRoomLifecycle] event=owner_leave_deferred reason=active_game_transition leaveReason={LeaveReason} room={RoomId} map={MapId} uid={Uid} activeGame={ActiveGameRoomId}",
                    reason,
                    roomId,
                    room.MapId,
                    uid,
                    room.ActiveGameRoomId);
                return true;
            }

            await DeleteAsync(roomId, $"owner_leave:{reason}");
            return true;
        }

        var key = _redis.KeyTownRoom(roomId);
        await _redis.Db.HashDeleteAsync(MembersKey(key), uid);
        var remaining = await _redis.Db.HashLengthAsync(MembersKey(key));
        _logger.LogInformation(
            "[TownRoomLifecycle] event=participant_leave reason={Reason} room={RoomId} uid={Uid} remaining={Remaining}",
            reason,
            roomId,
            uid,
            remaining);
        return true;
    }

    public async Task<bool> BindSteamLobbyAsync(string roomId, string uid, string steamLobbyId)
    {
        var room = await GetAsync(roomId);
        if (room == null)
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=steam_lobby_bind_fail reason=room_not_found room={RoomId} uid={Uid} lobby={Lobby}",
                roomId,
                uid,
                steamLobbyId);
            return false;
        }

        if (!string.Equals(room.OwnerUid, uid, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=steam_lobby_bind_fail reason=not_owner room={RoomId} uid={Uid} owner={OwnerUid} lobby={Lobby}",
                roomId,
                uid,
                room.OwnerUid,
                steamLobbyId);
            return false;
        }

        await _redis.Db.HashSetAsync(_redis.KeyTownRoom(roomId), "steamLobbyId", steamLobbyId ?? "");
        _logger.LogInformation(
            "[TownRoomLifecycle] event=steam_lobby_bind room={RoomId} owner={OwnerUid} lobby={Lobby}",
            roomId,
            uid,
            steamLobbyId ?? "");
        return true;
    }

    public async Task<(bool ok, string error)> SetActiveGameRoomAsync(
        string roomId,
        string uid,
        string gameRoomId,
        string gameMapId,
        string gameTitle)
    {
        var room = await GetAsync(roomId);
        if (room == null)
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=active_game_set_fail reason=room_not_found room={RoomId} uid={Uid} gameRoom={GameRoomId}",
                roomId,
                uid,
                gameRoomId);
            return (false, "RoomNotFound");
        }

        if (!string.Equals(room.OwnerUid, uid, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[TownRoomLifecycle] event=active_game_set_fail reason=not_owner room={RoomId} uid={Uid} owner={OwnerUid} gameRoom={GameRoomId}",
                roomId,
                uid,
                room.OwnerUid,
                gameRoomId);
            return (false, "NotOwner");
        }

        if (string.IsNullOrWhiteSpace(gameRoomId))
            return await ClearActiveGameRoomAsync(roomId, uid);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _redis.Db.HashSetAsync(_redis.KeyTownRoom(roomId), new HashEntry[]
        {
            new("activeGameRoomId", gameRoomId),
            new("activeGameMapId", gameMapId ?? ""),
            new("activeGameTitle", string.IsNullOrWhiteSpace(gameTitle) ? gameMapId ?? "" : gameTitle),
            new("activeGameHostUid", uid),
            new("activeGameCreatedAt", nowMs)
        });
        await _redis.Db.KeyExpireAsync(_redis.KeyTownRoom(roomId), RoomTtl);

        _logger.LogInformation(
            "[TownRoomLifecycle] event=active_game_set room={RoomId} owner={OwnerUid} gameRoom={GameRoomId} gameMap={GameMapId} title={Title}",
            roomId,
            uid,
            gameRoomId,
            gameMapId ?? "",
            string.IsNullOrWhiteSpace(gameTitle) ? "-" : gameTitle);
        return (true, "");
    }

    public async Task<(bool ok, string error)> ClearActiveGameRoomAsync(string roomId, string uid)
    {
        var room = await GetAsync(roomId);
        if (room == null)
            return (false, "RoomNotFound");

        if (!string.Equals(room.OwnerUid, uid, StringComparison.OrdinalIgnoreCase))
            return (false, "NotOwner");

        var removed = await _redis.Db.HashDeleteAsync(_redis.KeyTownRoom(roomId), new RedisValue[]
        {
            "activeGameRoomId",
            "activeGameMapId",
            "activeGameTitle",
            "activeGameHostUid",
            "activeGameCreatedAt"
        });

        _logger.LogInformation(
            "[TownRoomLifecycle] event=active_game_clear room={RoomId} owner={OwnerUid} removedFields={RemovedFields}",
            roomId,
            uid,
            removed);
        return (true, "");
    }

    public async Task<(bool ok, string error)> ClearActiveGameRoomIfMatchesAsync(string roomId, string uid, string gameRoomId)
    {
        var room = await GetAsync(roomId);
        if (room == null)
            return (false, "RoomNotFound");

        if (!string.Equals(room.OwnerUid, uid, StringComparison.OrdinalIgnoreCase))
            return (false, "NotOwner");

        if (!string.Equals(room.ActiveGameRoomId, gameRoomId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "[TownRoomLifecycle] event=active_game_clear_skip reason=game_room_mismatch room={RoomId} owner={OwnerUid} activeGame={ActiveGameRoomId} closingGame={ClosingGameRoomId}",
                roomId,
                uid,
                room.ActiveGameRoomId,
                gameRoomId);
            return (true, "");
        }

        return await ClearActiveGameRoomAsync(roomId, uid);
    }

    public async Task<TownRoomDto?> GetAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return null;

        var key = _redis.KeyTownRoom(roomId);
        if (!await _redis.Db.KeyExistsAsync(key))
            return null;

        var meta = await _redis.Db.HashGetAllAsync(key);
        var dict = meta.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var members = await _redis.Db.HashGetAllAsync(MembersKey(key));

        var dto = new TownRoomDto
        {
            RoomId = roomId,
            Title = dict.GetValueOrDefault("title") ?? "",
            MapId = dict.GetValueOrDefault("mapId") ?? "",
            MaxPlayers = int.TryParse(dict.GetValueOrDefault("maxPlayers") ?? "", out var max) ? max : DefaultMaxPlayers,
            OwnerUid = dict.GetValueOrDefault("ownerUid") ?? "",
            HostUid = dict.GetValueOrDefault("hostUid") ?? "",
            Status = dict.GetValueOrDefault("status") ?? "",
            IsPublic = ParseBool(dict.GetValueOrDefault("isPublic"), true),
            SteamLobbyId = dict.GetValueOrDefault("steamLobbyId") ?? "",
            ActiveGameRoomId = dict.GetValueOrDefault("activeGameRoomId") ?? "",
            ActiveGameMapId = dict.GetValueOrDefault("activeGameMapId") ?? "",
            ActiveGameTitle = dict.GetValueOrDefault("activeGameTitle") ?? "",
            ActiveGameHostUid = dict.GetValueOrDefault("activeGameHostUid") ?? "",
            ActiveGameCreatedAtMs = long.TryParse(dict.GetValueOrDefault("activeGameCreatedAt") ?? "", out var activeGameCreated) ? activeGameCreated : 0,
            CreatedAtMs = long.TryParse(dict.GetValueOrDefault("createdAt") ?? "", out var created) ? created : 0
        };

        foreach (var member in members)
        {
            var parsed = DeserializeParticipant(member.Value);
            if (parsed == null)
                continue;
            if (string.IsNullOrWhiteSpace(parsed.Uid))
                parsed.Uid = member.Name.ToString();
            dto.Participants.Add(parsed);
        }

        dto.Participants = OrderParticipants(dto).ToList();
        return dto;
    }

    public async Task<(List<TownRoomDto> rooms, string nextCursor)> GetListAsync(string mapId, int limit, string cursor)
    {
        if (limit <= 0) limit = 20;
        if (limit > 50) limit = 50;
        if (string.IsNullOrWhiteSpace(cursor)) cursor = "0";

        var indexKey = _redis.KeyTownRoomIndex();
        var result = await _redis.Db.ExecuteAsync("SSCAN", indexKey, cursor, "COUNT", limit);
        var resArr = (RedisResult[]?)result ?? Array.Empty<RedisResult>();
        if (resArr.Length < 2)
            return (new List<TownRoomDto>(), "0");

        var nextCursor = resArr[0].ToString() ?? "0";
        var ids = (RedisResult[]?)resArr[1] ?? Array.Empty<RedisResult>();

        var rooms = new List<TownRoomDto>();
        foreach (var id in ids)
        {
            var roomId = id.ToString();
            if (string.IsNullOrWhiteSpace(roomId))
                continue;

            var room = await GetAsync(roomId);
            if (room == null)
            {
                await DeleteAsync(roomId, "stale_index");
                continue;
            }

            if (!IsRoomListable(room, mapId))
                continue;

            rooms.Add(room);
        }

        rooms = await PruneDuplicateOwnerRoomsAsync(rooms);
        rooms = rooms
            .OrderByDescending(x => x.CreatedAtMs)
            .ToList();

        return (rooms, nextCursor);
    }

    public async Task<TownMatchManifest?> CreateOrReplaceManifestAsync(
        string roomId,
        string networkMode,
        string protocolVersion,
        CancellationToken ct = default)
    {
        var room = await GetAsync(roomId);
        if (room == null)
            return null;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ordered = OrderParticipants(room).ToList();
        var host = ordered.FirstOrDefault(x => string.Equals(x.Uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase))
                   ?? ordered.FirstOrDefault();

        if (host == null || string.IsNullOrWhiteSpace(host.Uid))
            return null;

        var participants = new List<TownMatchParticipant>();
        var nextActorId = FirstTownActorId;
        foreach (var participant in ordered)
        {
            if (string.IsNullOrWhiteSpace(participant.Uid))
                continue;

            participants.Add(new TownMatchParticipant
            {
                Uid = participant.Uid,
                DisplayName = participant.Name ?? "",
                SteamId64 = participant.SteamId64 ?? "",
                ActorId = nextActorId++,
                LoadoutHash = ""
            });
        }

        var manifest = new TownMatchManifest
        {
            MatchId = $"{room.RoomId}:{nowMs}",
            RoomId = room.RoomId,
            NetworkMode = string.IsNullOrWhiteSpace(networkMode) ? "steam_town_p2p_host" : networkMode,
            ProtocolVersion = string.IsNullOrWhiteSpace(protocolVersion) ? "0.0.0" : protocolVersion,
            MapId = room.MapId ?? "",
            StageSeed = 0,
            SongStartDelayMs = 1000,
            HostUid = host.Uid,
            HostSteamId64 = host.SteamId64 ?? "",
            HostEpoch = 1,
            PreferredHostRttMs = -1,
            HostSelectionMode = "owner_locked",
            HostSelectionMetricVersion = "town-owner-host-v1",
            HostSelectionEpoch = 1,
            HostSelectionScore = 1f,
            HostSelectionUpdatedAtMs = nowMs,
            HostCandidateOrder = participants.Select(x => x.Uid).ToList(),
            CreatedAtMs = nowMs,
            Participants = participants
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await _redis.Db.StringSetAsync(_redis.KeyTownMatchManifest(room.RoomId), json, expiry: RoomTtl);
        await _redis.Db.HashSetAsync(_redis.KeyTownRoom(room.RoomId), new HashEntry[]
        {
            new("hostUid", manifest.HostUid),
            new("status", "Open")
        });

        _logger.LogInformation(
            "[TownRoom] Stored manifest room={RoomId} host={HostUid} participants={Participants}",
            room.RoomId,
            manifest.HostUid,
            string.Join(",", manifest.Participants.Select(x => $"{x.ActorId}:{x.Uid}")));

        return manifest;
    }

    public async Task<TownMatchManifest?> GetManifestAsync(string roomId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return null;

        try
        {
            var json = await _redis.Db.StringGetAsync(_redis.KeyTownMatchManifest(roomId));
            if (json.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<TownMatchManifest>(json!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TownRoom] Failed to read manifest room={RoomId}", roomId);
            return null;
        }
    }

    private async Task CloseActiveRoomsForOwnerAsync(string ownerUid, string exceptRoomId = "")
    {
        if (string.IsNullOrWhiteSpace(ownerUid))
            return;

        var roomIds = await GetIndexedRoomIdsAsync();
        foreach (var roomId in roomIds)
        {
            if (string.Equals(roomId, exceptRoomId, StringComparison.OrdinalIgnoreCase))
                continue;

            var room = await GetAsync(roomId);
            if (room == null)
            {
                await DeleteAsync(roomId, "stale_index");
                continue;
            }

            if (!string.Equals(room.OwnerUid, ownerUid, StringComparison.OrdinalIgnoreCase))
                continue;

            await DeleteAsync(roomId, "owner_replaced");
            _logger.LogInformation("[TownRoom] Closed previous active room={RoomId} owner={OwnerUid}", roomId, ownerUid);
        }
    }

    private async Task<List<TownRoomDto>> PruneDuplicateOwnerRoomsAsync(List<TownRoomDto> rooms)
    {
        var kept = new List<TownRoomDto>();
        foreach (var group in rooms.GroupBy(x => x.OwnerUid, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group
                .OrderByDescending(x => x.CreatedAtMs)
                .ToList();

            var newest = ordered.FirstOrDefault();
            if (newest == null)
                continue;

            kept.Add(newest);
            foreach (var stale in ordered.Skip(1))
            {
                await DeleteAsync(stale.RoomId, "duplicate_owner");
                _logger.LogInformation("[TownRoom] Pruned duplicate owner room={RoomId} owner={OwnerUid}", stale.RoomId, stale.OwnerUid);
            }
        }

        return kept;
    }

    private async Task<List<string>> GetIndexedRoomIdsAsync()
    {
        var values = await _redis.Db.SetMembersAsync(_redis.KeyTownRoomIndex());
        return values
            .Where(x => x.HasValue)
            .Select(x => x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsRoomListable(TownRoomDto room, string mapId)
    {
        if (!string.Equals(room.Status, "Open", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!room.IsPublic)
            return false;
        if (!string.IsNullOrWhiteSpace(mapId) && !string.Equals(room.MapId, mapId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(room.OwnerUid))
            return false;
        if (room.Participants == null || room.Participants.Count == 0)
            return false;

        return room.Participants.Any(x => string.Equals(x.Uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase));
    }

    private async Task DeleteAsync(string roomId, string reason)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return;

        var key = _redis.KeyTownRoom(roomId);
        var mapId = (string?)await _redis.Db.HashGetAsync(key, "mapId") ?? "";
        var ownerUid = (string?)await _redis.Db.HashGetAsync(key, "ownerUid") ?? "";
        var memberCount = await _redis.Db.HashLengthAsync(MembersKey(key));
        var removedMeta = await _redis.Db.KeyDeleteAsync(key);
        var removedMembers = await _redis.Db.KeyDeleteAsync(MembersKey(key));
        var removedManifest = await _redis.Db.KeyDeleteAsync(_redis.KeyTownMatchManifest(roomId));
        await _redis.Db.SetRemoveAsync(_redis.KeyTownRoomIndex(), roomId);
        _logger.LogInformation(
            "[TownRoomLifecycle] event=room_delete reason={Reason} room={RoomId} map={MapId} owner={OwnerUid} participants={Participants} removedMeta={RemovedMeta} removedMembers={RemovedMembers} removedManifest={RemovedManifest}",
            reason,
            roomId,
            string.IsNullOrWhiteSpace(mapId) ? "-" : mapId,
            string.IsNullOrWhiteSpace(ownerUid) ? "-" : ownerUid,
            memberCount,
            removedMeta,
            removedMembers,
            removedManifest);
    }

    private static bool IsImplicitDisconnectReason(string reason)
    {
        return string.Equals(reason, "tcp_disconnect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, "server_disconnect", StringComparison.OrdinalIgnoreCase)
            || reason.StartsWith("lease_invalid:", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<TownRoomParticipantDto> OrderParticipants(TownRoomDto room)
    {
        return (room.Participants ?? new List<TownRoomParticipantDto>())
            .OrderByDescending(x => string.Equals(x.Uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.JoinedAtMs)
            .ThenBy(x => x.Uid, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<TownRoomParticipantDto?> GetParticipantAsync(string membersKey, string uid)
    {
        var val = await _redis.Db.HashGetAsync(membersKey, uid);
        return DeserializeParticipant(val);
    }

    private static TownRoomParticipantDto? DeserializeParticipant(RedisValue value)
    {
        if (!value.HasValue)
            return null;

        try
        {
            return JsonSerializer.Deserialize<TownRoomParticipantDto>(value.ToString(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string MembersKey(string roomKey) => $"{roomKey}:members";

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (bool.TryParse(value, out var parsed))
            return parsed;

        if (int.TryParse(value, out var intValue))
            return intValue != 0;

        return defaultValue;
    }
}
