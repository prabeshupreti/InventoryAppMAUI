using Grpc.Core;
using Grpc.Core.Interceptors;
using InventoryApp.Application.Common;

namespace InventoryApp.Api.Infrastructure;

/// <summary>
/// Translates application exceptions into gRPC status codes exactly once, so no service
/// implementation needs its own try/catch. Unexpected exceptions are logged in full and
/// reported to the client as a generic message.
/// </summary>
public sealed class ExceptionInterceptor(ILogger<ExceptionInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;   // already a well-formed gRPC fault
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ConflictException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (AuthenticationFailedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            // Domain invariants (e.g. negative stock) surface as a business rule violation.
            logger.LogWarning(ex, "Domain rule violated in {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error in {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Internal,
                "An unexpected error occurred. Please try again or contact support."));
        }
    }
}
