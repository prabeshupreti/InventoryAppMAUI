namespace InventoryApp.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "InventoryApp";
    public string Audience { get; set; } = "InventoryApp.Client";

    /// <summary>Must be at least 32 characters. Override via configuration or an environment variable in production.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480;
}
