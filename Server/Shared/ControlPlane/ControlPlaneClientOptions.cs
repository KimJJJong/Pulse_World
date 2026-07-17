namespace Shared.ControlPlane;

public sealed class ControlPlaneClientOptions
{
    //public ControlPlaneClientOptions(string address, string secret) { Address = address; Secret = secret; }
    public string Address { get; set; } = "";  // 예: https://cp-server:50051
    public string Secret { get; set; } = "";   // x-cp-secret
}
