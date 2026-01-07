using ControlPlane.Grpc.V1;
using Shared.ControlPlane;

namespace Server.Runtime;

public sealed class TownRoleModule : IRoleModule
{
    public string Name => "Town";
    public int DefaultTickMs => 100;
    public bool NeedsContentInit => false;
    public ServerType ToServerType() => ServerType.Town;
}
