using System.Globalization;
using InventoryApp.Contracts.Common;

namespace InventoryApp.Application.Common;

/// <summary>
/// The contracts carry timestamps as ISO-8601 strings, which keeps the wire format
/// human-readable and avoids Timestamp/DateTime kind mismatches on mobile clients.
/// </summary>
public static class TimeConversion
{
    public const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public static string ToIso(this DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString(Format, CultureInfo.InvariantCulture);

    public static string? ToIsoOrNull(this DateTime? value) => value?.ToIso();

    public static DateTime ParseUtc(string? value, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : fallback;
    }

    public static DateTime? ParseUtcOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    public static (DateTime? From, DateTime? To) Resolve(DateRange? range)
    {
        if (range is null)
        {
            return (null, null);
        }

        var from = ParseUtcOrNull(range.HasFromUtc ? range.FromUtc : null);
        var to = ParseUtcOrNull(range.HasToUtc ? range.ToUtc : null);

        // Treat the "to" bound as inclusive of the whole day when no time component was sent.
        if (to is { TimeOfDay.Ticks: 0 })
        {
            to = to.Value.AddDays(1).AddTicks(-1);
        }

        return (from, to);
    }
}
