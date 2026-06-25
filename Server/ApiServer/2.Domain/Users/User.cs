using ApiServer.Domain.Auth;

namespace ApiServer.Domain.Users;

public sealed class User
{
    // string UID를 기본으로 (나중에 snowflake/uuid로 바꿔도 됨)
    public string Uid { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public UserStatus Status { get; private set; } = UserStatus.Active;
    public int AppearanceId { get; private set; }

    // Navigation
    public List<UserIdentity> Identities { get; private set; } = new();
    public List<RefreshToken> RefreshTokens { get; private set; } = new();

    private User() { } // EF

    public User(string uid, DateTimeOffset now)
    {
        Uid = uid;
        CreatedAt = now;
        LastLoginAt = now;
        Status = UserStatus.Active;
        AppearanceId = 0;
    }

    public void MarkLogin(DateTimeOffset now) => LastLoginAt = now;

    public void Ban() => Status = UserStatus.Banned;

    public void SetAppearanceId(int appearanceId) => AppearanceId = appearanceId;
}
