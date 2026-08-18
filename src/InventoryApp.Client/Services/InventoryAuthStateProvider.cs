using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InventoryApp.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace InventoryApp.Client.Services;

/// <summary>
/// Turns the stored JWT into a ClaimsPrincipal so &lt;AuthorizeView&gt; and [Authorize]
/// work in the Blazor layer against exactly the same permission claims the API enforces.
/// </summary>
public sealed class InventoryAuthStateProvider(
    ITokenStore tokenStore,
    InventoryApiClient api,
    ILogger<InventoryAuthStateProvider> logger) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public UserDto? CurrentUser { get; private set; }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokenStore.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonymous;
        }

        var principal = BuildPrincipal(token);
        if (principal is null)
        {
            await tokenStore.ClearAsync();
            return Anonymous;
        }

        return new AuthenticationState(principal);
    }

    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var response = await api.LoginAsync(username, password, ct);

        await tokenStore.SaveTokenAsync(response.AccessToken);
        CurrentUser = response.User;
        api.ClearCaches();

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return true;
    }

    /// <summary>
    /// Sign-out is purely client side: the token is discarded and caches are dropped.
    /// The JWT is short-lived and stateless, so there is no server session to revoke.
    /// </summary>
    public async Task LogoutAsync()
    {
        await tokenStore.ClearAsync();
        CurrentUser = null;
        api.ClearCaches();

        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    /// <summary>Called when a call fails with Unauthenticated so the UI drops back to the login screen.</summary>
    public Task HandleSessionExpiredAsync() => LogoutAsync();

    private ClaimsPrincipal? BuildPrincipal(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            // Expiry is checked locally so an obviously stale token never triggers a round trip.
            if (jwt.ValidTo <= DateTime.UtcNow)
            {
                logger.LogInformation("Stored token expired at {Expiry}", jwt.ValidTo);
                return null;
            }

            var identity = new ClaimsIdentity(jwt.Claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stored token could not be parsed.");
            return null;
        }
    }
}
