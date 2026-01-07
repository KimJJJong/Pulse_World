using ApiServer.Domain.Users;

namespace ApiServer.Domain.Auth;

public sealed class RefreshToken
{
    // 서버가 발급한 refresh token 식별자 (jti 개념)
    public string TokenId { get; private set; } = "";

    public string Uid { get; private set; } = "";

    // refresh 원문은 저장하지 않고 hash만 저장
    public string TokenHash { get; private set; } = "";

    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenId { get; private set; }

    public string? DeviceId { get; private set; }
    public string? Ip { get; private set; }
    public string? UserAgent { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private RefreshToken() { } // EF

    public RefreshToken(
        string tokenId,
        string uid,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string? deviceId,
        string? ip,
        string? userAgent)
    {
        TokenId = tokenId;
        Uid = uid;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        DeviceId = deviceId;
        Ip = ip;
        UserAgent = userAgent;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;

    public void Revoke(DateTimeOffset now, string? replacedByTokenId)
    {
        if (RevokedAt.HasValue) return;
        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
