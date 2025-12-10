using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Rooms;

namespace Interface;    

public interface IRoomRepository
{
    ValueTask<Room> GetAsync(string roomId);
    ValueTask<IReadOnlyList<Room>> GetAllAsync(int pageSize, string cursor);
    ValueTask<(string cursor, IReadOnlyList<Room> rooms)> GetPagedAsync(int pageSize, string cursor);
    ValueTask<Room> CreateAsync(Room r);
    ValueTask<bool> DeleteAsync(string roomId);
    ValueTask<bool> TryJoinAsync(string roomId, Member m); // 원자적 Join
    ValueTask<bool> LeaveAsync(string roomId, string userId);
    ValueTask UpdateAsync(Room r);
}

public interface IRoomReadModel
{
    ValueTask<(string etag, IReadOnlyList<Room>)> GetSnapshotWithEtagAsync(int pageSize, string cursor);
}

public interface IRoomService
{
    Task<(Room room, string wsUrl, string token)> CreateAndJoinAsync(string title, string map, int max, RoomVisibility vis, string userId, string userName);
    Task<(Room room, string wsUrl, string token)> JoinAsync(string roomId, string userId, string userName);
}



public interface IJwtService
{
    (string token, string jti, string nonce) IssueTicket(IDictionary<string, object> claims, TimeSpan ttl);
    (bool ok, IDictionary<string, object> dict, string code) ValidateTicket(string token);

    string IssueRoomToken(string roomId, string userId, TimeSpan ttl);
    (bool ok, string userId, string roomId) ValidateRoomToken(string token);
}

public interface ITicketIssuer
{
    string IssueGameTicket(string roomId, IEnumerable<string> userIds, TimeSpan ttl);
}
