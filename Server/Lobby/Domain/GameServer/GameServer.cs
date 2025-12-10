namespace Lobby.Domain.GameServer;


public enum GameServerStatus
{
    Unknown = 0,
    Active =1,      // can Get New Room
    Draining =2,    // awaken but, before get Room
    Down =3         // Dead
}

public sealed class GameServerInfo
{
    /// <summary>GameServer IP ( ex :"gs1" )</summary>
    public string Id { get; init; } = default!;

    /// <summary>Public Host what Client Connect ( ex "jjongserver.ddns.net" )</summary>
    public string Host {  get; init; } = default!;
    /// <summary>Port chat Client Connect ( may be : 13000~13050 )</summary>
    public int Port { get; init; }
    /// <summary>Max Room Repository</summary>
    public int MaxRooms { get; init; }

    /// <summary>Running Room Count</summary>
    public int CurrentRooms { get; init; }

    /// <summary>Stat (Active/Draining/Down etc... )</summary>
    public GameServerStatus Status { get; init; } = GameServerStatus.Active;

    /// <summary>Last Heartbeat Time (UTC)</summary>
    public DateTimeOffset LastHeartbeat { get; init; }
}

public interface IGameServerRegistry
{
    /// <summary>
    ///  regist or update "gs" information in Redis
    ///  using when GameServer booting/heartbit, Orchestrator change stat
    /// </summary>
    Task RegisterOrUpdateAsync(GameServerInfo info, CancellationToken ct = default);

    /// <summary>get specify gsId. When it was empty be a null.</summary>
    Task<GameServerInfo?> GetAsync(string gsId, CancellationToken ct = default);

    /// <summary>
    /// Get overall GameServer Information.
    /// ( Handling with Make inner index set)
    /// </summary>
    Task<IReadOnlyList<GameServerInfo>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Remove Server (gs:* key del)
    /// </summary>
    Task RemoveAsync(string gsId, CancellationToken ct = default);
}