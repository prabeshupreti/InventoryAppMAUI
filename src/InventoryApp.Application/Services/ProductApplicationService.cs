using System.Linq.Expressions;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

public sealed class ProductApplicationService(
    IInventoryDbContext db,
    IStockLedger stockLedger,
    ILogger<ProductApplicationService> logger) : IProductApplicationService
{
    private static readonly Dictionary<string, Expression<Func<Product, object>>> SortMap = new()
    {
        ["sku"] = p => p.Sku,
        ["name"] = p => p.Name,
        ["categoryName"] = p => p.Category!.Name,
        ["supplierName"] = p => p.Supplier!.CompanyName,
        ["unitPrice"] = p => p.UnitPrice,
        ["costPrice"] = p => p.CostPrice,
        ["currentStock"] = p => p.CurrentStock,
        ["createdAtUtc"] = p => p.CreatedAtUtc,
        ["updatedAtUtc"] = p => p.UpdatedAtUtc
    };

    public async Task<ListProductsResponse> ListAsync(ListProductsRequest request, CancellationToken ct)
    {
        var query = BuildQuery(request);
        query = Paging.ApplySort(query, request.Page, SortMap, "name");

        var (items, info) = await Paging.ToPageAsync(query, request.Page, ct);

        var response = new ListProductsResponse { PageInfo = info };
        response.Items.AddRange(items.Select(p => p.ToDto()));
        return response;
    }

    private IQueryable<Product> BuildQuery(ListProductsRequest request)
    {
        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsQueryable();

        var search = Paging.SearchTerm(request.Page);
        if (search.Length > 0)
        {
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{search}%") ||
                EF.Functions.Like(p.Sku, $"%{search}%") ||
                EF.Functions.Like(p.Barcode, $"%{search}%") ||
                EF.Functions.Like(p.Description, $"%{search}%"));
        }

        if (request.HasCategoryId && request.CategoryId > 0)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId);
        }

        if (request.HasSupplierId && request.SupplierId > 0)
        {
            query = query.Where(p => p.SupplierId == request.SupplierId);
        }

        if (request.HasOnlyActive)
        {
            query = query.Where(p => p.IsActive == request.OnlyActive);
        }

        if (request.HasStockStatus && request.StockStatus != StockStatus.Unspecified)
        {
            // Evaluated in SQL rather than in memory so paging stays server-side.
            query = request.StockStatus switch
            {
                StockStatus.OutOfStock => query.Where(p => p.CurrentStock <= 0),
                StockStatus.LowStock => query.Where(p => p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock),
                StockStatus.InStock => query.Where(p => p.CurrentStock > p.MinimumStock),
                _ => query
            };
        }

        return query;
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);

        return product.ToDto();
    }

    public async Task<ProductDto> GetBySkuAsync(string sku, CancellationToken ct)
    {
        var value = Guard.Required(sku, "SKU or barcode", 100);

        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Sku == value || p.Barcode == value, ct)
            ?? throw new ValidationException($"No product found for '{value}'.");

        return product.ToDto();
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        var sku = Guard.Required(request.Sku, "SKU", 60).ToUpperInvariant();
        var name = Guard.Required(request.Name, "Product name", 200);

        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
        {
            throw new ConflictException($"SKU '{sku}' is already in use.");
        }

        await ValidateRelationsAsync(request.CategoryId, request.SupplierId, ct);
        ValidateNumbers(request.UnitPrice, request.CostPrice, request.MinimumStock, request.MaximumStock);
        Guard.NotNegative(request.OpeningStock, "Opening stock");

        var barcode = Guard.Optional(request.Barcode, "Barcode", 60);
        if (barcode.Length > 0 && await db.Products.AnyAsync(p => p.Barcode == barcode, ct))
        {
            throw new ConflictException($"Barcode '{barcode}' is already assigned to another product.");
        }

        var product = new Product
        {
            Sku = sku,
            Name = name,
            Description = Guard.Optional(request.Description, "Description", 1000),
            CategoryId = request.CategoryId,
            SupplierId = request.SupplierId,
            UnitPrice = request.UnitPrice.ToMoney(),
            CostPrice = request.CostPrice.ToMoney(),
            MinimumStock = request.MinimumStock,
            MaximumStock = request.MaximumStock,
            UnitOfMeasure = Guard.Optional(request.UnitOfMeasure, "Unit of measure", 20) is { Length: > 0 } uom ? uom : "pcs",
            Barcode = barcode,
            ImageUrl = Guard.Optional(request.ImageUrl, "Image URL", 500),
            Location = Guard.Optional(request.Location, "Location", 120) is { Length: > 0 } loc ? loc : "Main Warehouse",
            IsActive = request.IsActive
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        // An opening balance is still a stock movement, so the ledger stays complete from day one.
        if (request.OpeningStock > 0)
        {
            await stockLedger.RecordAsync(
                product,
                DomainEnums.MovementType.StockIn,
                request.OpeningStock,
                "Opening stock",
                $"NEW-{product.Sku}",
                ct);

            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Product {Sku} created", product.Sku);
        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
                      ?? throw new NotFoundException("Product", request.Id);

        var sku = Guard.Required(request.Sku, "SKU", 60).ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Id != request.Id && p.Sku == sku, ct))
        {
            throw new ConflictException($"SKU '{sku}' is already in use.");
        }

        var barcode = Guard.Optional(request.Barcode, "Barcode", 60);
        if (barcode.Length > 0 && await db.Products.AnyAsync(p => p.Id != request.Id && p.Barcode == barcode, ct))
        {
            throw new ConflictException($"Barcode '{barcode}' is already assigned to another product.");
        }

        await ValidateRelationsAsync(request.CategoryId, request.SupplierId, ct);
        ValidateNumbers(request.UnitPrice, request.CostPrice, request.MinimumStock, request.MaximumStock);

        product.Sku = sku;
        product.Name = Guard.Required(request.Name, "Product name", 200);
        product.Description = Guard.Optional(request.Description, "Description", 1000);
        product.CategoryId = request.CategoryId;
        product.SupplierId = request.SupplierId;
        product.UnitPrice = request.UnitPrice.ToMoney();
        product.CostPrice = request.CostPrice.ToMoney();
        product.MinimumStock = request.MinimumStock;
        product.MaximumStock = request.MaximumStock;
        product.UnitOfMeasure = Guard.Optional(request.UnitOfMeasure, "Unit of measure", 20) is { Length: > 0 } uom ? uom : "pcs";
        product.Barcode = barcode;
        product.ImageUrl = Guard.Optional(request.ImageUrl, "Image URL", 500);
        product.Location = Guard.Optional(request.Location, "Location", 120) is { Length: > 0 } loc ? loc : "Main Warehouse";
        product.IsActive = request.IsActive;
        product.Touch();

        await db.SaveChangesAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<OperationResult> DeleteAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Product", id);

        if (await db.PurchaseOrderItems.AnyAsync(i => i.ProductId == id, ct) ||
            await db.SaleItems.AnyAsync(i => i.ProductId == id, ct))
        {
            throw new ConflictException(
                $"'{product.Name}' appears on purchase or sales documents and cannot be deleted. " +
                "Deactivate it instead so history remains intact.");
        }

        if (product.CurrentStock > 0)
        {
            throw new ConflictException(
                $"'{product.Name}' still has {product.CurrentStock} unit(s) in stock. " +
                "Issue or write off the remaining stock before deleting it.");
        }

        // Movements are owned by the product and cascade-delete with it.
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Product {Sku} deleted", product.Sku);
        return new OperationResult { Success = true, Message = $"Product '{product.Name}' deleted." };
    }

    public async Task<LookupList> GetLookupAsync(ProductLookupRequest request, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().AsQueryable();

        if (request.HasOnlyActive)
        {
            query = query.Where(p => p.IsActive == request.OnlyActive);
        }

        var search = (request.Search ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{search}%") ||
                EF.Functions.Like(p.Sku, $"%{search}%") ||
                EF.Functions.Like(p.Barcode, $"%{search}%"));
        }

        var items = await query
            .OrderBy(p => p.Name)
            .Take(50)   // lookups feed a combobox; never ship the whole catalogue
            .Select(p => new LookupItem { Id = p.Id, Name = p.Sku + " - " + p.Name })
            .ToListAsync(ct);

        var list = new LookupList();
        list.Items.AddRange(items);
        return list;
    }

    private async Task ValidateRelationsAsync(int categoryId, int supplierId, CancellationToken ct)
    {
        Guard.Positive(categoryId, "Category");
        Guard.Positive(supplierId, "Supplier");

        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            throw new ValidationException("The selected category no longer exists.");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
        {
            throw new ValidationException("The selected supplier no longer exists.");
        }
    }

    private static void ValidateNumbers(double unitPrice, double costPrice, int minStock, int maxStock)
    {
        Guard.NotNegative(unitPrice.ToMoney(), "Unit price");
        Guard.NotNegative(costPrice.ToMoney(), "Cost price");
        Guard.NotNegative(minStock, "Minimum stock");
        Guard.NotNegative(maxStock, "Maximum stock");

        if (maxStock > 0 && maxStock < minStock)
        {
            throw new ValidationException("Maximum stock cannot be lower than minimum stock.");
        }
    }
}
