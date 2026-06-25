using Server.Domain.Connections;

namespace Server.Presentation.Tcp;

/// <summary>
/// 네 TCP 프레임워크(Listener/UserToken 등)의 연결 종료 시점에서 호출.
/// </summary>
public sealed class ConnectionLifecycleHooks
{
    private readonly ConnectionRegistry _registry;

    public ConnectionLifecycleHooks(ConnectionRegistry registry)
    {
        _registry = registry;
    }

    public void OnDisconnected(ITcpConnection conn)
    {
        // conn에 저장된 uid/epoch를 꺼낼 수 있어야 함.
        // (BindAuth에서 저장했으니 conn이 제공해야 함)
        if (conn is IAuthedTcpConnection authed && authed.HasAuth)
        {
            _registry.UnbindIfMatch(authed.Uid, conn.ConnId, authed.Epoch);
        }
    }
}

/// <summary>
/// ITcpConnection 구현체가 uid/epoch를 제공하도록 확장(권장).
/// </summary>
public interface IAuthedTcpConnection : ITcpConnection
{
    bool HasAuth { get; }
    string Uid { get; }
    long Epoch { get; }
}
