using InventoryApp.Domain.Enums;

namespace InventoryApp.Domain.Entities;

/// <summary>Immutable audit record. One row per stock change, never updated or deleted.</summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public MovementType MovementType { get; set; }

    /// <summary>Signed quantity: positive for stock-in, negative for stock-out, 0 for transfers.</summary>
    public int Quantity { get; set; }

    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;

    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
