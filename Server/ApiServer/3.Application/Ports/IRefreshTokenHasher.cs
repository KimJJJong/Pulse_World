namespace ApiServer.Application.Ports;

public interface IRefreshTokenHasher
{
    string Hash(string refreshTokenPlaintext);
}
