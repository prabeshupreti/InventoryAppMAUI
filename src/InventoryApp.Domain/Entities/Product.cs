using InventoryApp.Domain.Enums;

namespace InventoryApp.Domain.Entities;

public class Product : AuditableEntity
{
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }

    /// <summary>
    /// Only mutated through <see cref="ApplyStockDelta"/> / <see cref="SetStock"/> so that
    /// no code path can change quantity without the caller also writing a StockMovement.
    /// </summary>
    public int CurrentStock { get; private set; }

    public int MinimumStock { get; set; }
    public int MaximumStock { get; set; }
    public string UnitOfMeasure { get; set; } = "pcs";
    public string Barcode { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Location { get; set; } = "Main Warehouse";
    public bool IsActive { get; set; } = true;

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public StockStatus StockStatus =>
        CurrentStock <= 0 ? StockStatus.OutOfStock
        : CurrentStock <= MinimumStock ? StockStatus.LowStock
        : StockStatus.InStock;

    public decimal StockValue => CurrentStock * CostPrice;

    /// <summary>Applies a signed change and returns the previous quantity.</summary>
    public int ApplyStockDelta(int delta)
    {
        var previous = CurrentStock;
        var next = previous + delta;
        if (next < 0)
        {
            throw new InvalidOperationException(
                $"Stock for '{Name}' cannot go negative. Available: {previous}, requested change: {delta}.");
        }

        CurrentStock = next;
        Touch();
        return previous;
    }

    /// <summary>Sets an absolute quantity (physical count) and returns the previous quantity.</summary>
    public int SetStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new InvalidOperationException("Counted quantity cannot be negative.");
        }

        var previous = CurrentStock;
        CurrentStock = quantity;
        Touch();
        return previous;
    }

    /// <summary>Used by the seeder and EF materialisation paths that legitimately set an opening balance.</summary>
    public void SetOpeningStock(int quantity) => CurrentStock = quantity < 0 ? 0 : quantity;
}
