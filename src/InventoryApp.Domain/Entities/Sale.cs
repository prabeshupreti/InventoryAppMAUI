using InventoryApp.Domain.Enums;

namespace InventoryApp.Domain.Entities;

public class Sale : AuditableEntity
{
    public required string SaleNumber { get; set; }

    public string CustomerName { get; set; } = "Walk-in customer";
    public DateTime SaleDateUtc { get; set; } = DateTime.UtcNow;
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }

    public string Notes { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    public void Recalculate()
    {
        foreach (var item in Items)
        {
            item.LineTotal = decimal.Round(item.Quantity * item.UnitPrice, 2);
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

public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
