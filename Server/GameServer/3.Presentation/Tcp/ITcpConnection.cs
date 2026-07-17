using System.Threading;
using System.Threading.Tasks;

public interface ITcpConnection
{
    string ConnId { get; }
    bool IsConnected { get; }
    CancellationToken ConnectionToken { get; }

    void BindAuth(string uid, long epoch, string key);

    Task SendHandshakeOkAsync(string uid, long epoch, int serverRole, string key);
    Task SendHandshakeFailAsync(string reason);

    void Close(string reason);
}
