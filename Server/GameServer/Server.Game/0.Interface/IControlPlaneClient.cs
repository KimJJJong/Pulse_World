//using ControlPlane.Grpc.V1;
//using System.Threading;
//using System.Threading.Tasks;

//public sealed record TicketVerifyResult(bool Ok, string? Uid, string? Ctx, int ErrorCode, string ErrorMsg);
//public sealed record AttachResult(bool Ok, long Epoch, int ErrorCode, string ErrorMsg);

//public interface IControlPlaneClient
//{
//    Task<TicketVerifyResult> ReserveOrConsumeTicketAsync(
//        string ticketId,
//        TicketTarget expectedTarget,
//        string verifierServerId,
//        string connId,
//        string clientNonce,
//        CancellationToken ct);

//    Task<AttachResult> AttachConnectionAsync(
//        string uid,
//        ConnState state,
//        string connId,
//        int leaseTtlSec,
//        CancellationToken ct);

//    Task<bool> RenewLeaseAsync(
//        string uid,
//        ConnState state,
//        string connId,
//        long epoch,
//        int leaseTtlSec,
//        CancellationToken ct);
//}
