namespace InventoryApp.Client.Services;

/// <summary>Persists the access token between app launches.</summary>
public interface ITokenStore
{
    Task<string?> GetTokenAsync();
    Task SaveTokenAsync(string token);
    Task ClearAsync();

    /// <summary>Cached synchronously for the gRPC interceptor, which runs on the call path.</summary>
    string? CurrentToken { get; }
}

/// <summary>
/// Backed by MAUI SecureStorage (Keychain on iOS/macOS, Keystore on Android, DPAPI on Windows).
/// Falls back to Preferences if the platform keystore is unavailable, which can happen on
/// some Android emulator images without a secure lock screen.
/// </summary>
public sealed class SecureTokenStore(ILogger<SecureTokenStore> logger) : ITokenStore
{
    private const string Key = "inventoryapp.access_token";
    private string? _cached;

    public string? CurrentToken => _cached;

    public async Task<string?> GetTokenAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            _cached = await SecureStorage.Default.GetAsync(Key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SecureStorage unavailable; falling back to Preferences.");
            _cached = Preferences.Default.Get<string?>(Key, null);
        }

        return _cached;
    }

    public async Task SaveTokenAsync(string token)
    {
        _cached = token;

        try
        {
            await SecureStorage.Default.SetAsync(Key, token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SecureStorage unavailable; falling back to Preferences.");
            Preferences.Default.Set(Key, token);
        }
    }

    public Task ClearAsync()
    {
        _cached = null;
        SecureStorage.Default.Remove(Key);
        Preferences.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
