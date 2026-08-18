using System.Security.Claims;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Security;
using InventoryApp.Domain.Enums;

namespace InventoryApp.Api.Infrastructure;

/// <summary>Reads the caller identity from the validated JWT on the current request.</summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int UserId =>
        int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public string UserName =>
        Principal?.FindFirstValue("fullName")
        ?? Principal?.FindFirstValue(ClaimTypes.Name)
        ?? "System";

    public UserRole Role =>
        (UserRole)(int)RolePermissions.FromRoleName(Principal?.FindFirstValue(ClaimTypes.Role));
}
