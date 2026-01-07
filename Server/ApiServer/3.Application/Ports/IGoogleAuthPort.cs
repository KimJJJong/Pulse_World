namespace ApiServer.Application.Ports;

public interface IGoogleAuthPort
{
    Task<(bool ok, string subject, string email)> VerifyAsync(
        string authCode,
        string redirectUri,
        CancellationToken ct);
}
