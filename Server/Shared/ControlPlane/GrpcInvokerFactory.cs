using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace Shared.ControlPlane;

public static class GrpcInvokerFactory
{
    // 채널은 비싸다 → DI Singleton으로 1번만 만들 것
    public static CallInvoker CreateControlPlaneInvoker(ControlPlaneClientOptions opt)
    {
        if (string.IsNullOrWhiteSpace(opt.Address))
            throw new ArgumentException("ControlPlane.Address is required");
        if (string.IsNullOrWhiteSpace(opt.Secret))
            throw new ArgumentException("ControlPlane.Secret is required");

        var channel = GrpcChannel.ForAddress(opt.Address, new GrpcChannelOptions
        {
            ServiceConfig = new ServiceConfig
            {
                MethodConfigs =
                {
                    new MethodConfig
                    {
                        Names = { MethodName.Default },
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = 3,
                            InitialBackoff = TimeSpan.FromMilliseconds(100),
                            MaxBackoff = TimeSpan.FromMilliseconds(500),
                            BackoffMultiplier = 2,
                            RetryableStatusCodes = { Grpc.Core.StatusCode.Unavailable }
                        }
                    }
                }
            }
        });

        return channel.Intercept(new CpAuthInterceptor(opt.Secret));
    }
}
