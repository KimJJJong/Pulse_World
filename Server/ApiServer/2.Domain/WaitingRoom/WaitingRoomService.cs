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
    public List<string> MemberUids { get; set; } = new();
    public Dictionary<string, bool> MemberReady { get; set; } = new();
}

public sealed class WaitingRoomService
{
    private readonly RedisStore _redis;

    public WaitingRoomService(RedisStore redis)
    {
        _redis = redis;
    }

    public async Task<string?> CreateAsync(string title, string mapId, int maxPlayers, string ownerUid, string ownerName)
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
            new("createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        };

        await _redis.Db.HashSetAsync(key, entries);
        await _redis.Db.KeyExpireAsync(key, TimeSpan.FromHours(1)); 

        // Add index
        await _redis.Db.SetAddAsync(_redis.KeyWaitingRoomIndex(), roomId);

        // Add owner
        await JoinAsync(roomId, ownerUid, ownerName); 
        
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

        // If already member, update name? or Just return OK
        if (await _redis.Db.HashExistsAsync(membersKey, uid))
        {
             // Already joined
             return (true, "");
        }

        if (count >= max) return (false, "RoomFull");

        var memberData = JsonSerializer.Serialize(new { Name = name, Ready = false });
        await _redis.Db.HashSetAsync(membersKey, uid, memberData);
        
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
        
        var val = await _redis.Db.HashGetAsync(membersKey, uid);
        if (!val.HasValue) return false;

        try 
        {
            using var doc = JsonDocument.Parse(val.ToString());
            var name = "";
            if(doc.RootElement.TryGetProperty("Name", out var n)) name = n.GetString() ?? "";

            var newData = JsonSerializer.Serialize(new { Name = name, Ready = ready });
            await _redis.Db.HashSetAsync(membersKey, uid, newData);
            return true;
        }
        catch { return false; }
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
            MemberUids = new List<string>(),
            MemberReady = new Dictionary<string, bool>()
        };

        foreach(var m in members)
        {
             var uid = m.Name.ToString();
             dto.MemberUids.Add(uid);
             try {
                using var doc = JsonDocument.Parse(m.Value.ToString());
                 if(doc.RootElement.TryGetProperty("Ready", out var r))
                    dto.MemberReady[uid] = r.GetBoolean();
                 else 
                    dto.MemberReady[uid] = false;
             } catch {
                 dto.MemberReady[uid] = false;
             }
        }
        
        return (true, dto);
    }

    public async Task<(List<WaitingRoomDto> rooms, string nextCursor)> GetListAsync(int limit, string cursor)
    {
        if (limit <= 0) limit = 10;
        if (string.IsNullOrEmpty(cursor)) cursor = "0";

        var result = await _redis.Db.ExecuteAsync("SSCAN", _redis.KeyWaitingRoomIndex(), cursor, "COUNT", limit);
        var resArr = (RedisResult[])result;

        var nextCursor = (string)resArr[0];
        var ids = (RedisResult[])resArr[1];

        var tasks = new List<Task<(bool, WaitingRoomDto?)>>();
        foreach (var id in ids)
        {
            tasks.Add(GetAsync((string)id));
        }

        var results = await Task.WhenAll(tasks);
        var list = new List<WaitingRoomDto>();
        foreach (var (ok, dto) in results)
        {
            if (ok && dto != null) list.Add(dto);
        }

        return (list, nextCursor);
    }
}
