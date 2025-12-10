/*using System.Collections.Generic;
using Server.withWebServer.Security;
using Interface;
public static class TicketValidator
{
    public static IJwtService Jwt = null!;
    public static void Init(IJwtService jwt) => Jwt = jwt;

    public static (bool ok, IDictionary<string, object>? claims, string code) Validate(string token)
        => Jwt.ValidateTicket(token);  // RS256/HS256은 appsettings에 따라 자동
}
*/