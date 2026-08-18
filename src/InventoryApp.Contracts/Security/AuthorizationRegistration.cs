using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Contracts.Security;

/// <summary>
/// Registers one policy per permission. Shared by the API and the Blazor client so
/// [Authorize(Policy = Permissions.X)] and &lt;AuthorizeView Policy="..."&gt; behave identically.
/// </summary>
public static class AuthorizationRegistration
{
    public static AuthorizationOptions AddInventoryPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in Permissions.All)
        {
            options.AddPolicy(permission, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(Permissions.ClaimType, permission));
        }

        return options;
    }
}
