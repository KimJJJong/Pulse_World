using Lobby.Domain.Auth.Services;
using Lobby.Domain.Rooms;
using Lobby.Infrastructure.Security;
using Contracts.Packet;


namespace Lobby.Domain.Auth.Interface;

public interface IRoomRepository
{
    ValueTask<Room?> GetAsync(string roomId);
    ValueTask<IReadOnlyList<Room>> GetAllAsync(int pageSize, string? cursor);
    ValueTask<(string cursor, IReadOnlyList<Room> rooms)> GetPagedAsync(int pageSize, string? cursor);
    ValueTask<Room> CreateAsync(Room r);
    ValueTask<bool> DeleteAsync(string roomId);
    ValueTask<bool> TryJoinAsync(string roomId, Member m); // 원자적 Join
    ValueTask<bool> LeaveAsync(string roomId, string userId);
    ValueTask UpdateAsync(Room r);
}

public interface IRoomReadModel
{
    ValueTask<(string etag, IReadOnlyList<Room>)> GetSnapshotWithEtagAsync(int pageSize, string? cursor);
}

public interface IRoomService
{
    Task<(Room room, string wsUrl, string token)> CreateAndJoinAsync(string title, string map, int max, RoomVisibility vis, string userId, string userName);
    Task<(Room room, string wsUrl, string token)> JoinAsync(string roomId, string userId, string userName);
}

public interface IRoomBroadcaster
{
    Task PublishRoomUpdateAsync(Room r);
    Task PublishMemberUpdateAsync(Room r, Member m);
    Task PublishCountdownAsync(Room r, bool start, int? seconds, long? startAtMs);
    Task PublishGameBeginAsync(Room r, string host, int port, string ticket);
}

public interface IUserRepository
{
    Task UpsertGuestAsync(string userId, string userName);
    Task UpsertGoogleAsync(string userId, string displayName);

}

public interface IJwtService
{
    // GameServer 이동용 검증 티켓 ( Redis에 저장 : 내부 메모리 )
    (string token,string jti, string nonce) IssueTicket(IDictionary<string, object> claims, TimeSpan ttl);
    (bool ok, IDictionary<string, object>? dict, string code) ValidateTicket(string token);

    // AccessToken : 그대로 RS256 서명
    (string token, DateTime expireIn, string jti) IssueAccessToken(string userId, IDictionary<string, object> customClaims, TimeSpan ttl);
    (bool ok, string? userId, IDictionary<string, object>? claims, string? code) ValidateAccessToken(string token);

}

// GameStart Ticket
public interface ITicketIssuer
{
    IReadOnlyList<(string uid, int slot, GameStartTicket ticket)> IssueStartTickets(
    string matchId, string roomId,
    IReadOnlyList<(string uid, int slot)> players,
    string gsHost, int gsPort,
    int tickRate, TimeSpan ttl,
    int proto);
}

public interface IRefreshTokenService
{
    Task<(string plain, Guid familyId, DateTimeOffset expiresAt)> IssueAsync(string userId, Guid? familyId = null, string? ip = null, string? ua = null);
    Task<(bool ok, string userId, long tokenId, Guid familyId, DateTimeOffset expiresAt)?> ValidateAsync(string plain);

    Task<(string newToken, Guid familyId, DateTimeOffset expiresAt)> 
        RotateAsync(string userId, long oldTokenId, Guid familyId, string? ip, string? ua);
    Task RevokeAsync(string plain, string reason);
    Task RevokeFamilyAsync(Guid familyId, string reason);


}
public interface IRefreshTokenRepository
{
    Task InsertAsync(string userId, byte[] hash, byte[] salt, Guid familyId, DateTimeOffset expiresAt, string? ip, string? ua);
    Task<(long id, string userId, byte[] hash, byte[] salt, Guid familyId, DateTimeOffset expiresAt, DateTimeOffset? revokedAt)?>
        FindByPlainAsync (string plain);

    Task RevokeAsync(long id, string reason);
    Task RevokeFamilyAsync(Guid familyId, string reason);


}
public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> VerifyAsync(string idToken);
    public string GetAuthUrl(string state);
    Task<GoogleTokenResponse> ExchangeCodeAsync(string code);
    Task<GoogleUserInfo> GetUserInfoAsync(string googleAccessToken);

}

public interface IMatchStore
{
    Task CreateAsync(string matchId, string gsId, string roomId, string uidA, string uidB, TimeSpan? ttl = null);
    Task SetFieldsAsync(string matchId, IDictionary<string, string> fields, TimeSpan? ttl = null);
    Task<Dictionary<string, string>> GetAllAsync(string matchId);
    Task<bool> ExistsAsync(string matchId);
}
