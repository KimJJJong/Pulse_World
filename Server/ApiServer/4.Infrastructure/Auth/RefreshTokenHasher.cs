using ApiServer.Application.Ports;
using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Security;
using Microsoft.Extensions.Options;

namespace ApiServer.Infrastructure.Auth;

public sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    private readonly SecurityOptions _opt;

    public RefreshTokenHasher(IOptions<SecurityOptions> opt)
    {
        _opt = opt.Value;
    }

    public string Hash(string refreshTokenPlaintext)
    {
        // pepper를 옵션으로 둘 수도 있지만, 운영에서는 환경변수/Secret로 두는 걸 추천.
        // 지금은 옵션에 Pepper 필드가 없으니 "없으면 빈 문자열"로 처리.
        // (나중에 SecurityOptions에 Pepper 추가해도 됨)
        const string pepper = "";
        return Hashing.Sha256HexWithPepper(refreshTokenPlaintext, pepper);
    }
}
