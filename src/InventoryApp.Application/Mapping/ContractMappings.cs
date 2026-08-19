using InventoryApp.Application.Common;
using InventoryApp.Contracts.Auth;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Stock;
using InventoryApp.Contracts.Trade;
using InventoryApp.Domain.Entities;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Mapping;

/// <summary>Entity -> contract projections. Entities never cross the service boundary.</summary>
public static class ContractMappings
{
    // Money travels as double on the wire; it is stored and computed as decimal server-side.
    public static double ToWire(this decimal value) => (double)decimal.Round(value, 2);

    public static decimal ToMoney(this double value) => decimal.Round((decimal)value, 2);

    public static UserDto ToDto(this User user)
    {
        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Role = (UserRole)(int)user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc.ToIso()
        };

        if (user.LastLoginAtUtc.HasValue)
        {
            dto.LastLoginAtUtc = user.LastLoginAtUtc.Value.ToIso();
        }

        return dto;
    }

    public static CategoryDto ToDto(this Category category, int productCount) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IsActive = category.IsActive,
        ProductCount = productCount,
        CreatedAtUtc = category.CreatedAtUtc.ToIso(),
        UpdatedAtUtc = category.UpdatedAtUtc.ToIso()
    };

    public static SupplierDto ToDto(this Supplier supplier, int productCount) => new()
    {
        Id = supplier.Id,
        CompanyName = supplier.CompanyName,
        ContactPerson = supplier.ContactPerson,
        Email = supplier.Email,
        Phone = supplier.Phone,
        Address = supplier.Address,
        Notes = supplier.Notes,
        IsActive = supplier.IsActive,
        ProductCount = productCount,
        CreatedAtUtc = supplier.CreatedAtUtc.ToIso(),
        UpdatedAtUtc = supplier.UpdatedAtUtc.ToIso()
    };

    public static ProductDto ToDto(this Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        CategoryId = product.CategoryId,
        CategoryName = product.Category?.Name ?? string.Empty,
        SupplierId = product.SupplierId,
        SupplierName = product.Supplier?.CompanyName ?? string.Empty,
        UnitPrice = product.UnitPrice.ToWire(),
        CostPrice = product.CostPrice.ToWire(),
        CurrentStock = product.CurrentStock,
        MinimumStock = product.MinimumStock,
        MaximumStock = product.MaximumStock,
        UnitOfMeasure = product.UnitOfMeasure,
        Barcode = product.Barcode,
        ImageUrl = product.ImageUrl,
        Location = product.Location,
        IsActive = product.IsActive,
        StockStatus = (StockStatus)(int)product.StockStatus,
        StockValue = product.StockValue.ToWire(),
        CreatedAtUtc = product.CreatedAtUtc.ToIso(),
        UpdatedAtUtc = product.UpdatedAtUtc.ToIso()
    };

    public static StockMovementDto ToDto(this StockMovement movement) => new()
    {
        Id = movement.Id,
        ProductId = movement.ProductId,
        ProductSku = movement.Product?.Sku ?? string.Empty,
        ProductName = movement.Product?.Name ?? string.Empty,
        MovementType = (MovementType)(int)movement.MovementType,
        Quantity = movement.Quantity,
        PreviousQuantity = movement.PreviousQuantity,
        NewQuantity = movement.NewQuantity,
        Reason = movement.Reason,
        Reference = movement.Reference,
        UserId = movement.UserId,
        UserName = movement.UserName,
        FromLocation = movement.FromLocation,
        ToLocation = movement.ToLocation,
        CreatedAtUtc = movement.CreatedAtUtc.ToIso()
    };

    public static PurchaseItemDto ToDto(this PurchaseOrderItem item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductSku = item.Product?.Sku ?? string.Empty,
        ProductName = item.Product?.Name ?? string.Empty,
        Quantity = item.Quantity,
        UnitCost = item.UnitCost.ToWire(),
        LineTotal = item.LineTotal.ToWire()
    };

    public static PurchaseOrderDto ToDto(this PurchaseOrder order)
    {
        var dto = new PurchaseOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.CompanyName ?? string.Empty,
            OrderDateUtc = order.OrderDateUtc.ToIso(),
            Status = (PurchaseStatus)(int)order.Status,
            Subtotal = order.Subtotal.ToWire(),
            TaxRate = order.TaxRate.ToWire(),
            TaxAmount = order.TaxAmount.ToWire(),
            DiscountAmount = order.DiscountAmount.ToWire(),
            Total = order.Total.ToWire(),
            Notes = order.Notes,
            CreatedBy = order.CreatedByName,
            CreatedAtUtc = order.CreatedAtUtc.ToIso()
        };

        // Optional protobuf fields reject null. Set them only when there's a value;
        // otherwise the HasExpectedDateUtc / HasReceivedDateUtc flags stay false.
        if (order.ExpectedDateUtc.HasValue)
        {
            dto.ExpectedDateUtc = order.ExpectedDateUtc.Value.ToIso();
        }

        if (order.ReceivedDateUtc.HasValue)
        {
            dto.ReceivedDateUtc = order.ReceivedDateUtc.Value.ToIso();
        }

        dto.Items.AddRange(order.Items.Select(i => i.ToDto()));
        return dto;
    }

    public static SaleItemDto ToDto(this SaleItem item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductSku = item.Product?.Sku ?? string.Empty,
        ProductName = item.Product?.Name ?? string.Empty,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice.ToWire(),
        LineTotal = item.LineTotal.ToWire()
    };

    public static SaleDto ToDto(this Sale sale)
    {
        var dto = new SaleDto
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            CustomerName = sale.CustomerName,
            SaleDateUtc = sale.SaleDateUtc.ToIso(),
            Status = (SaleStatus)(int)sale.Status,
            Subtotal = sale.Subtotal.ToWire(),
            TaxRate = sale.TaxRate.ToWire(),
            TaxAmount = sale.TaxAmount.ToWire(),
            DiscountAmount = sale.DiscountAmount.ToWire(),
            Total = sale.Total.ToWire(),
            Notes = sale.Notes,
            CreatedBy = sale.CreatedByName
        };

        dto.Items.AddRange(sale.Items.Select(i => i.ToDto()));
        return dto;
    }

    public static DomainEnums.MovementType ToDomain(this MovementType type) =>
        (DomainEnums.MovementType)(int)type;

    public static DomainEnums.StockStatus ToDomain(this StockStatus status) =>
        (DomainEnums.StockStatus)(int)status;

    public static DomainEnums.UserRole ToDomain(this UserRole role) =>
        (DomainEnums.UserRole)(int)role;

    public static DomainEnums.PurchaseStatus ToDomain(this PurchaseStatus status) =>
        (DomainEnums.PurchaseStatus)(int)status;

    public static DomainEnums.SaleStatus ToDomain(this SaleStatus status) =>
        (DomainEnums.SaleStatus)(int)status;
}
