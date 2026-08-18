using InventoryApp.Domain.Enums;

namespace InventoryApp.Domain.Entities;

public class PurchaseOrder : AuditableEntity
{
    public required string OrderNumber { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime OrderDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDateUtc { get; set; }
    public DateTime? ReceivedDateUtc { get; set; }

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }

    public string Notes { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();

    public bool IsEditable => Status == PurchaseStatus.Draft;
    public bool CanBeReceived => Status is PurchaseStatus.Draft or PurchaseStatus.Ordered;

    /// <summary>Recomputes subtotal/tax/total from the current item lines.</summary>
    public void Recalculate()
    {
        foreach (var item in Items)
        {
            item.LineTotal = decimal.Round(item.Quantity * item.UnitCost, 2);
        }

        Subtotal = decimal.Round(Items.Sum(i => i.LineTotal), 2);

        var discount = DiscountAmount < 0 ? 0 : DiscountAmount;
        if (discount > Subtotal)
        {
            discount = Subtotal;
        }

        DiscountAmount = decimal.Round(discount, 2);
        var taxable = Subtotal - DiscountAmount;
        TaxAmount = decimal.Round(taxable * (TaxRate / 100m), 2);
        Total = decimal.Round(taxable + TaxAmount, 2);
    }
}

public class PurchaseOrderItem
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
