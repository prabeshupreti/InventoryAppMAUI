using InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Abstractions;

/// <summary>Ambient information about the caller, resolved per request from the JWT.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    string UserName { get; }
    UserRole Role { get; }
}
