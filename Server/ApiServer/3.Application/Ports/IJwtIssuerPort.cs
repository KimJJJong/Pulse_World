using System.Security.Claims;

namespace ApiServer.Application.Ports;

public interface IJwtIssuerPort
{
    string IssueAccessToken(string uid, IEnumerable<Claim> extraClaims);
}
