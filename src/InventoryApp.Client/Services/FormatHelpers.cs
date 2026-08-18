using System.Globalization;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Stock;
using InventoryApp.Contracts.Trade;
using Microsoft.FluentUI.AspNetCore.Components;

namespace InventoryApp.Client.Services;

/// <summary>Presentation-only helpers. Keeps formatting out of the Razor markup.</summary>
public static class FormatHelpers
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static string Money(double value) => value.ToString("N2", Culture);

    public static string MoneyCompact(double value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000).ToString("N1", Culture) + "M",
        >= 1_000 => (value / 1_000).ToString("N1", Culture) + "K",
        _ => value.ToString("N0", Culture)
    };

    public static string Number(int value) => value.ToString("N0", Culture);

    public static string Date(string? isoUtc) =>
        DateTime.TryParse(isoUtc, Culture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt.ToLocalTime().ToString("dd MMM yyyy", Culture)
            : "-";

    public static string DateTimeShort(string? isoUtc) =>
        DateTime.TryParse(isoUtc, Culture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt.ToLocalTime().ToString("dd MMM yyyy, HH:mm", Culture)
            : "-";

    public static DateTime? ToDateTime(string? isoUtc) =>
        DateTime.TryParse(isoUtc, Culture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt.ToLocalTime()
            : null;

    public static string ToIso(DateTime? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", Culture) ?? string.Empty;

    public static string StockStatusLabel(StockStatus status) => status switch
    {
        StockStatus.InStock => "In stock",
        StockStatus.LowStock => "Low stock",
        StockStatus.OutOfStock => "Out of stock",
        _ => "Unknown"
    };

    /// <summary>Maps a status onto a Fluent design token so badges stay on-palette.</summary>
    public static string StockStatusColor(StockStatus status) => status switch
    {
        StockStatus.InStock => "var(--success)",
        StockStatus.LowStock => "var(--warning)",
        StockStatus.OutOfStock => "var(--error)",
        _ => "var(--neutral-foreground-hint)"
    };

    public static Appearance StockStatusAppearance(StockStatus status) => status switch
    {
        StockStatus.InStock => Appearance.Accent,
        StockStatus.LowStock => Appearance.Neutral,
        StockStatus.OutOfStock => Appearance.Neutral,
        _ => Appearance.Neutral
    };

    public static string MovementLabel(MovementType type) => type switch
    {
        MovementType.StockIn => "Stock in",
        MovementType.StockOut => "Stock out",
        MovementType.Adjustment => "Adjustment",
        MovementType.PurchaseReceipt => "Purchase receipt",
        MovementType.Sale => "Sale",
        MovementType.SaleReturn => "Sale return",
        MovementType.Transfer => "Transfer",
        _ => "Unknown"
    };

    public static string MovementColor(MovementType type) => type switch
    {
        MovementType.StockIn or MovementType.PurchaseReceipt or MovementType.SaleReturn => "var(--success)",
        MovementType.StockOut or MovementType.Sale => "var(--error)",
        MovementType.Adjustment => "var(--warning)",
        _ => "var(--neutral-foreground-hint)"
    };

    public static string PurchaseStatusLabel(PurchaseStatus status) => status switch
    {
        PurchaseStatus.Draft => "Draft",
        PurchaseStatus.Ordered => "Ordered",
        PurchaseStatus.Received => "Received",
        PurchaseStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    public static string PurchaseStatusColor(PurchaseStatus status) => status switch
    {
        PurchaseStatus.Received => "var(--success)",
        PurchaseStatus.Ordered => "var(--info)",
        PurchaseStatus.Cancelled => "var(--error)",
        _ => "var(--neutral-foreground-hint)"
    };

    public static string SaleStatusLabel(SaleStatus status) =>
        status == SaleStatus.Cancelled ? "Cancelled" : "Completed";

    public static string SaleStatusColor(SaleStatus status) =>
        status == SaleStatus.Cancelled ? "var(--error)" : "var(--success)";

    public static string Signed(int quantity) => quantity > 0 ? $"+{Number(quantity)}" : Number(quantity);
}
