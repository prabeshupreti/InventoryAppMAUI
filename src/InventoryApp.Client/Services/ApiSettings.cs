namespace InventoryApp.Client.Services;

/// <summary>
/// Resolves the backend address per platform. The Android emulator reaches the host
/// machine through 10.0.2.2; simulators and desktop targets can use localhost directly.
/// Set <see cref="OverrideBaseAddress"/> (or edit this class) when deploying to a real device.
/// </summary>
public static class ApiSettings
{
    private const int HttpsPort = 7266;
    private const int HttpPort = 5266;

    /// <summary>Set this to e.g. "https://192.168.1.20:7266" when testing on a physical device.</summary>
    public static string? OverrideBaseAddress { get; set; }

    public static string BaseAddress
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(OverrideBaseAddress))
            {
                return OverrideBaseAddress!;
            }

#if ANDROID
            // Emulator loopback alias. A physical device must use the host machine's LAN IP.
            return DeviceInfo.DeviceType == DeviceType.Virtual
                ? $"https://10.0.2.2:{HttpsPort}"
                : $"https://{LanHostFallback}:{HttpsPort}";
#elif IOS
            return DeviceInfo.DeviceType == DeviceType.Virtual
                ? $"https://localhost:{HttpsPort}"
                : $"https://{LanHostFallback}:{HttpsPort}";
#else
            return $"https://localhost:{HttpsPort}";
#endif
        }
    }

    /// <summary>Change this to your development machine's LAN IP for physical device testing.</summary>
    public const string LanHostFallback = "192.168.1.100";

    public static string HttpBaseAddress => BaseAddress.Replace($":{HttpsPort}", $":{HttpPort}")
                                                       .Replace("https://", "http://");

    /// <summary>Read-heavy lookups are cached briefly to cut chatter on mobile networks.</summary>
    public static TimeSpan LookupCacheLifetime { get; } = TimeSpan.FromMinutes(5);
}
