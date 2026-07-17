using ControlPlane.Grpc.V1;

namespace Server.Runtime;

public interface IRoleModule
{
    string Name { get; }
    int DefaultTickMs { get; }
    bool NeedsContentInit { get; }

    // register payload etc 필요하면 여기로
    ServerType ToServerType();
}
