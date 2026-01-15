namespace ApiServer.Infrastructure.Options;

public sealed class SecurityOptions
{
    public int RefreshTokenDays { get; init; } = 14;
    public int RefreshTokenLength { get; init; } = 64;

    // refresh 재사용 감지 시 정책
    public bool RevokeAllOnReuse { get; init; } = true;
}
