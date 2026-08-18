using Grpc.Core;
using Grpc.Core.Interceptors;

namespace InventoryApp.Client.Services;

/// <summary>
/// Attaches the bearer token to every outgoing call so no client service has to remember to.
/// </summary>
public sealed class AuthInterceptor(ITokenStore tokenStore) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var token = tokenStore.CurrentToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            return continuation(request, context);
        }

        var headers = context.Options.Headers ?? new Metadata();
        if (context.Options.Headers is null)
        {
            context = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, context.Options.WithHeaders(headers));
        }

        headers.Add("Authorization", $"Bearer {token}");
        return continuation(request, context);
    }
}
