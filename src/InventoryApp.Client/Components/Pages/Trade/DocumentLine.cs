namespace InventoryApp.Client.Components.Pages.Trade;

/// <summary>Editable line shared by the purchase and sales editors.</summary>
public class DocumentLine
{
    public string? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2);
}

public sealed class PurchaseLine : DocumentLine;

public sealed class SaleLine : DocumentLine
{
    /// <summary>Stock on hand at the time the product was picked, used for an inline warning.</summary>
    public int AvailableStock { get; set; }
}
