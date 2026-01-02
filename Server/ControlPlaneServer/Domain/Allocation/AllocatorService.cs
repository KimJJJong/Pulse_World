using ControlPlane.Grpc.V1;

namespace ControlPlane.Domain.Allocation;

public sealed class AllocatorService
{
    public string PickServerId(TicketTarget target, string preferredServerId)
    {
        if (!string.IsNullOrWhiteSpace(preferredServerId))
            return preferredServerId;

        return target switch
        {
            TicketTarget.Town => "town-1",
            TicketTarget.Game => "game-1",
            _ => "unknown-1"
        };
    }
}
