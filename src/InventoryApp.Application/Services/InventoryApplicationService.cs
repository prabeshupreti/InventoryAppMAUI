using System.Linq.Expressions;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Stock;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

public sealed class InventoryApplicationService(
    IInventoryDbContext db,
    IStockLedger stockLedger,
    ICurrentUser currentUser,
    ILogger<InventoryApplicationService> logger) : IInventoryApplicationService
{
    private static readonly Dictionary<string, Expression<Func<StockMovement, object>>> SortMap = new()
    {
        ["createdAtUtc"] = m => m.CreatedAtUtc,
        ["productName"] = m => m.Product!.Name,
        ["movementType"] = m => m.MovementType,
        ["quantity"] = m => m.Quantity,
        ["userName"] = m => m.UserName
    };

    public async Task<ListMovementsResponse> ListMovementsAsync(ListMovementsRequest request, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking()
            .Include(m => m.Product)
            .AsQueryable();

        var search = Paging.SearchTerm(request.Page);
        if (search.Length > 0)
        {
            query = query.Where(m =>
                EF.Functions.Like(m.Product!.Name, $"%{search}%") ||
                EF.Functions.Like(m.Product!.Sku, $"%{search}%") ||
                EF.Functions.Like(m.Reason, $"%{search}%") ||
                EF.Functions.Like(m.Reference, $"%{search}%"));
        }

        if (request.HasProductId && request.ProductId > 0)
        {
            query = query.Where(m => m.ProductId == request.ProductId);
        }

        if (request.HasCategoryId && request.CategoryId > 0)
        {
            query = query.Where(m => m.Product!.CategoryId == request.CategoryId);
        }

        if (request.HasMovementType && request.MovementType != MovementType.Unspecified)
        {
            var type = request.MovementType.ToDomain();
            query = query.Where(m => m.MovementType == type);
        }

        var (from, to) = TimeConversion.Resolve(request.DateRange);
        if (from.HasValue) query = query.Where(m => m.CreatedAtUtc >= from.Value);
        if (to.HasValue) query = query.Where(m => m.CreatedAtUtc <= to.Value);

        // Newest first is the useful default for an audit log.
        query = string.IsNullOrWhiteSpace(request.Page?.SortBy)
            ? query.OrderByDescending(m => m.CreatedAtUtc)
            : Paging.ApplySort(query, request.Page, SortMap, "createdAtUtc");

        var (items, info) = await Paging.ToPageAsync(query, request.Page, ct);

        var response = new ListMovementsResponse { PageInfo = info };
        response.Items.AddRange(items.Select(m => m.ToDto()));
        return response;
    }

    public async Task<StockOperationResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct)
    {
        Guard.Positive(request.Quantity, "Quantity");
        var product = await LoadProductAsync(request.ProductId, ct);

        var movement = await stockLedger.RecordAsync(
            product,
            DomainEnums.MovementType.StockIn,
            request.Quantity,
            Guard.Required(request.Reason, "Reason", 250),
            Guard.Optional(request.Reference, "Reference", 100),
            ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Received {Qty} of {Sku} by {User}", request.Quantity, product.Sku, currentUser.UserName);

        return BuildResponse(product, movement);
    }

    public async Task<StockOperationResponse> IssueAsync(IssueStockRequest request, CancellationToken ct)
    {
        Guard.Positive(request.Quantity, "Quantity");
        var product = await LoadProductAsync(request.ProductId, ct);

        if (request.Quantity > product.CurrentStock)
        {
            throw new ConflictException(
                $"Cannot issue {request.Quantity} unit(s) of '{product.Name}'. Only {product.CurrentStock} in stock.");
        }

        var movement = await stockLedger.RecordAsync(
            product,
            DomainEnums.MovementType.StockOut,
            -request.Quantity,
            Guard.Required(request.Reason, "Reason", 250),
            Guard.Optional(request.Reference, "Reference", 100),
            ct);

        await db.SaveChangesAsync(ct);
        return BuildResponse(product, movement);
    }

    public async Task<StockOperationResponse> AdjustAsync(AdjustStockRequest request, CancellationToken ct)
    {
        Guard.NotNegative(request.CountedQuantity, "Counted quantity");
        var product = await LoadProductAsync(request.ProductId, ct);

        var delta = request.CountedQuantity - product.CurrentStock;
        if (delta == 0)
        {
            throw new ValidationException(
                $"The counted quantity already matches the recorded stock ({product.CurrentStock}). Nothing to adjust.");
        }

        var movement = await stockLedger.RecordAsync(
            product,
            DomainEnums.MovementType.Adjustment,
            delta,
            Guard.Required(request.Reason, "Adjustment reason", 250),
            Guard.Optional(request.Reference, "Reference", 100),
            ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Adjusted {Sku} by {Delta} to {New}", product.Sku, delta, product.CurrentStock);

        return BuildResponse(product, movement);
    }

    public async Task<StockOperationResponse> TransferAsync(TransferStockRequest request, CancellationToken ct)
    {
        Guard.Positive(request.Quantity, "Quantity");
        var toLocation = Guard.Required(request.ToLocation, "Destination location", 120);
        var product = await LoadProductAsync(request.ProductId, ct);

        if (string.Equals(product.Location, toLocation, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("The product is already at that location.");
        }

        if (request.Quantity > product.CurrentStock)
        {
            throw new ConflictException(
                $"Cannot transfer {request.Quantity} unit(s). Only {product.CurrentStock} in stock at {product.Location}.");
        }

        // A transfer relocates stock without changing the total, so the ledger delta is zero.
        var fromLocation = product.Location;
        var movement = new StockMovement
        {
            ProductId = product.Id,
            Product = product,
            MovementType = DomainEnums.MovementType.Transfer,
            Quantity = 0,
            PreviousQuantity = product.CurrentStock,
            NewQuantity = product.CurrentStock,
            Reason = Guard.Optional(request.Reason, "Reason", 250) is { Length: > 0 } r
                ? r
                : $"Transferred {request.Quantity} unit(s) from {fromLocation} to {toLocation}",
            Reference = string.Empty,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            FromLocation = fromLocation,
            ToLocation = toLocation
        };

        db.StockMovements.Add(movement);

        // The whole quantity moved, so the product's home location follows it.
        if (request.Quantity == product.CurrentStock)
        {
            product.Location = toLocation;
        }

        product.Touch();
        await db.SaveChangesAsync(ct);

        return BuildResponse(product, movement);
    }

    public async Task<ListMovementsResponse> GetProductHistoryAsync(ProductHistoryRequest request, CancellationToken ct)
    {
        Guard.Positive(request.ProductId, "Product");

        var listRequest = new ListMovementsRequest
        {
            Page = request.Page ?? new PageRequest { Page = 1, PageSize = Paging.DefaultPageSize },
            ProductId = request.ProductId,
            DateRange = request.DateRange
        };

        return await ListMovementsAsync(listRequest, ct);
    }

    public async Task<LowStockAlertResponse> GetLowStockAlertsAsync(CancellationToken ct)
    {
        var rows = await db.Products.AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.CurrentStock)
            .ThenBy(p => p.Name)
            .Take(100)
            .Select(p => new LowStockAlert
            {
                ProductId = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                CurrentStock = p.CurrentStock,
                MinimumStock = p.MinimumStock,
                SupplierName = p.Supplier!.CompanyName,
                OutOfStock = p.CurrentStock <= 0
            })
            .ToListAsync(ct);

        var response = new LowStockAlertResponse();
        response.Items.AddRange(rows);
        return response;
    }

    private async Task<Product> LoadProductAsync(int id, CancellationToken ct)
    {
        Guard.Positive(id, "Product");

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Product", id);

        if (!product.IsActive)
        {
            throw new ConflictException($"'{product.Name}' is inactive. Reactivate it before moving stock.");
        }

        return product;
    }

    private static StockOperationResponse BuildResponse(Product product, StockMovement movement) => new()
    {
        ProductId = product.Id,
        NewQuantity = product.CurrentStock,
        Movement = movement.ToDto()
    };
}
