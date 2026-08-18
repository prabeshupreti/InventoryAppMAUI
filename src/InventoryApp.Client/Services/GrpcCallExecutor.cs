using Grpc.Core;

namespace InventoryApp.Client.Services;

/// <summary>
/// Wraps every gRPC call so components deal with one exception type and readable messages
/// instead of raw RpcException plumbing.
/// </summary>
public sealed class GrpcCallExecutor(ILogger<GrpcCallExecutor> logger)
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> call, string operation)
    {
        try
        {
            return await call();
        }
        catch (RpcException ex)
        {
            var message = Describe(ex, operation);
            logger.LogWarning(ex, "gRPC {Operation} failed with {Status}", operation, ex.StatusCode);
            throw new ApiException(message, ex.StatusCode, ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure during {Operation}", operation);
            throw new ApiException(
                $"Could not complete {operation}. Please try again.", StatusCode.Unknown, ex);
        }
    }

    private static string Describe(RpcException ex, string operation) => ex.StatusCode switch
    {
        // The server's own message is already user-facing for these.
        StatusCode.InvalidArgument or StatusCode.FailedPrecondition or StatusCode.NotFound
            or StatusCode.AlreadyExists => ex.Status.Detail,

        StatusCode.Unauthenticated => "Your session has expired. Please sign in again.",
        StatusCode.PermissionDenied => "You do not have permission to perform this action.",
        StatusCode.Unavailable => "Cannot reach the inventory server. Check your connection and try again.",
        StatusCode.DeadlineExceeded => "The server took too long to respond. Please try again.",
        StatusCode.Cancelled => $"{operation} was cancelled.",
        _ => string.IsNullOrWhiteSpace(ex.Status.Detail)
            ? $"Could not complete {operation}. Please try again."
            : ex.Status.Detail
    };
}
