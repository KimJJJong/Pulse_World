namespace Shared.ControlPlane;

public sealed class ControlPlaneClientOptions
{
    public string Address { get; set; } = "";  // 예: https://cp-server:50051
    public string Secret { get; set; } = "";   // x-cp-secret
}
