namespace ApiServer.Shared.Abstractions;

public interface IIdGenerator
{
    string NewUid();
    string NewTokenId(); // refresh token_id(jti)
}
