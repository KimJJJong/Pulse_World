using ClientEndpoint = ApiServer.Application.Ports.Models.Endpoint;

namespace ApiServer.Presentation.Http;

public static class EndpointHostResolver
{
    public static ClientEndpoint ToClientReachableEndpoint(ClientEndpoint endpoint, HttpContext context)
    {
        if (!ShouldUseRequestHost(endpoint.Host))
            return endpoint;

        var requestHost = ResolveRequestHost(context);
        return string.IsNullOrWhiteSpace(requestHost)
            ? endpoint
            : endpoint with { Host = requestHost };
    }

    private static string ResolveRequestHost(HttpContext context)
    {
        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var rawHost = string.IsNullOrWhiteSpace(forwardedHost)
            ? context.Request.Host.Host
            : forwardedHost.Split(',')[0].Trim();

        if (HostString.FromUriComponent(rawHost).Host is { Length: > 0 } parsedHost)
            return parsedHost;

        return rawHost;
    }

    private static bool ShouldUseRequestHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;

        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::", StringComparison.OrdinalIgnoreCase);
    }
}
