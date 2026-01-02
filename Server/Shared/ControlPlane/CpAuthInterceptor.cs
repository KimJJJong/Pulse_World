using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Shared.ControlPlane;

public sealed class CpAuthInterceptor : Interceptor
{
    private readonly string _secret;
    private const string HeaderName = "x-cp-secret";

    public CpAuthInterceptor(string secret) => _secret = secret ?? "";

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();

        for (int i = headers.Count - 1; i >= 0; i--)
            if (string.Equals(headers[i].Key, HeaderName, StringComparison.OrdinalIgnoreCase))
                headers.RemoveAt(i);

        headers.Add(HeaderName, _secret);

        var options = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options);

        return continuation(request, newContext);
    }
}
