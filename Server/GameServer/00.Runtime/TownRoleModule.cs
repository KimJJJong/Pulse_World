using ControlPlane.Grpc.V1;
using Shared.ControlPlane;

namespace Server.Runtime;

public sealed class TownRoleModule : IRoleModule
{
    public string Name => "Town";
    // [RTT Fix] TownSnapshotProvider와 동기화: 100->33ms
    public int DefaultTickMs => 33;
    public bool NeedsContentInit => true;
    public ServerType ToServerType() => ServerType.TypeTown;
}
