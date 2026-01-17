using ControlPlane.Grpc.V1;
using Shared.ControlPlane;

namespace Server.Runtime;

public sealed class GameRoleModule : IRoleModule
{
    public string Name => "Game";
    public int DefaultTickMs => 15;
    public bool NeedsContentInit => true;
    public ServerType ToServerType() => ServerType.TypeGame;
}
