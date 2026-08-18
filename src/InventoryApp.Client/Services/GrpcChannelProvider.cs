using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace InventoryApp.Client.Services;

/// <summary>
/// Owns the single long-lived channel used by every generated client.
/// gRPC-Web is used rather than raw gRPC because Android and iOS ship HTTP stacks
/// without HTTP/2 trailer support, which native gRPC depends on.
/// </summary>
public sealed class GrpcChannelProvider : IDisposable
{
    private readonly AuthInterceptor _authInterceptor;
    private readonly ILogger<GrpcChannelProvider> _logger;
    private readonly object _gate = new();

    private GrpcChannel? _channel;
    private CallInvoker? _invoker;
    private string? _address;

    public GrpcChannelProvider(AuthInterceptor authInterceptor, ILogger<GrpcChannelProvider> logger)
    {
        _authInterceptor = authInterceptor;
        _logger = logger;
    }

    public CallInvoker Invoker
    {
        get
        {
            lock (_gate)
            {
                // Rebuild if the target address changed at runtime (e.g. user pointed at a LAN host).
                if (_invoker is not null && _address == ApiSettings.BaseAddress)
                {
                    return _invoker;
                }

                _channel?.Dispose();
                _address = ApiSettings.BaseAddress;
                _channel = CreateChannel(_address);
                _invoker = _channel.Intercept(_authInterceptor);

                _logger.LogInformation("gRPC channel created for {Address}", _address);
                return _invoker;
            }
        }
    }

    private static GrpcChannel CreateChannel(string address)
    {
        var httpHandler = new HttpClientHandler();

#if DEBUG
        // Development only: the ASP.NET Core self-signed certificate is not trusted by
        // emulators or simulators. Never ship this branch in a release build.
        httpHandler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif

        var webHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, httpHandler);

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = webHandler,
            MaxReceiveMessageSize = 8 * 1024 * 1024,
            MaxSendMessageSize = 4 * 1024 * 1024,
            ThrowOperationCanceledOnCancellation = true
        });
    }

    /// <summary>Forces the next call to build a fresh channel; used after changing the server address.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _channel?.Dispose();
            _channel = null;
            _invoker = null;
            _address = null;
        }
    }

    public void Dispose() => _channel?.Dispose();
}
