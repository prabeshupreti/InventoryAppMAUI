using Grpc.Core;

namespace InventoryApp.Client.Services;

/// <summary>A gRPC fault translated into something worth showing a user.</summary>
public sealed class ApiException(string message, StatusCode statusCode, Exception? inner = null)
    : Exception(message, inner)
{
    public StatusCode StatusCode { get; } = statusCode;

    /// <summary>True when the session has expired or the user lacks permission.</summary>
    public bool IsAuthFailure => StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied;

    public bool IsOffline => StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded;
}
