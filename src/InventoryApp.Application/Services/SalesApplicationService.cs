using System.Linq.Expressions;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Trade;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

public sealed class SalesApplicationService(
    IInventoryDbContext db,
    IStockLedger stockLedger,
    IDocumentNumberGenerator numbers,
    ICurrentUser currentUser,
    ILogger<SalesApplicationService> logger) : ISalesApplicationService
{
    private static readonly Dictionary<string, Expression<Func<Sale, object>>> SortMap = new()
    {
        ["saleNumber"] = s => s.SaleNumber,
        ["customerName"] = s => s.CustomerName,
        ["saleDateUtc"] = s => s.SaleDateUtc,
        ["status"] = s => s.Status,
        ["total"] = s => s.Total
    };

    public async Task<ListSalesResponse> ListAsync(ListSalesRequest request, CancellationToken ct)
    {
        var query = db.Sales.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        var search = Paging.SearchTerm(request.Page);
        if (search.Length > 0)
        {
            query = query.Where(s =>
                EF.Functions.Like(s.SaleNumber, $"%{search}%") ||
                EF.Functions.Like(s.CustomerName, $"%{search}%") ||
                EF.Functions.Like(s.Notes, $"%{search}%"));
        }

        if (request.HasStatus && request.Status != SaleStatus.Unspecified)
        {
            var status = request.Status.ToDomain();
            query = query.Where(s => s.Status == status);
        }

        var (from, to) = TimeConversion.Resolve(request.DateRange);
        if (from.HasValue) query = query.Where(s => s.SaleDateUtc >= from.Value);
        if (to.HasValue) query = query.Where(s => s.SaleDateUtc <= to.Value);

        query = string.IsNullOrWhiteSpace(request.Page?.SortBy)
            ? query.OrderByDescending(s => s.SaleDateUtc).ThenByDescending(s => s.Id)
            : Paging.ApplySort(query, request.Page, SortMap, "saleDateUtc");

        var (items, info) = await Paging.ToPageAsync(query, request.Page, ct);

        var response = new ListSalesResponse { PageInfo = info };
        response.Items.AddRange(items.Select(s => s.ToDto()));
        return response;
    }

    public async Task<SaleDto> GetAsync(int id, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Sale", id);

        return sale.ToDto();
    }

    public async Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken ct)
    {
        var lines = request.Items.ToList();
        if (lines.Count == 0)
        {
            throw new ValidationException("Add at least one product to the sale.");
        }

        var merged = lines
            .GroupBy(i => i.ProductId)
            .Select(g => (
                ProductId: g.Key,
                Quantity: g.Sum(x => x.Quantity),
                UnitPrice: g.Last().UnitPrice.ToMoney()))
            .ToList();

        foreach (var line in merged)
        {
            Guard.Positive(line.ProductId, "Product");
            Guard.Positive(line.Quantity, "Quantity");
            Guard.NotNegative(line.UnitPrice, "Unit price");
        }

        var ids = merged.Select(m => m.ProductId).ToList();

        // Tracked load: these entities have their stock decremented below.
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var sale = new Sale
        {
            SaleNumber = await numbers.NextSaleNumberAsync(ct),
            CustomerName = Guard.Optional(request.CustomerName, "Customer name", 200) is { Length: > 0 } c
                ? c
                : "Walk-in customer",
            SaleDateUtc = TimeConversion.ParseUtc(request.SaleDateUtc, DateTime.UtcNow),
            Status = DomainEnums.SaleStatus.Completed,
            TaxRate = ValidateRate(request.TaxRate),
            DiscountAmount = request.DiscountAmount.ToMoney(),
            Notes = Guard.Optional(request.Notes, "Notes", 1000),
            CreatedByUserId = currentUser.UserId,
            CreatedByName = currentUser.UserName
        };

        foreach (var line in merged)
        {
            var product = products.FirstOrDefault(p => p.Id == line.ProductId)
                          ?? throw new ValidationException($"Product {line.ProductId} no longer exists.");

            if (!product.IsActive)
            {
                throw new ValidationException($"'{product.Name}' is inactive and cannot be sold.");
            }

            // Overselling is rejected outright rather than allowed to create negative stock.
            if (line.Quantity > product.CurrentStock)
            {
                throw new ConflictException(
                    $"Only {product.CurrentStock} unit(s) of '{product.Name}' are in stock, but {line.Quantity} were requested.");
            }

            sale.Items.Add(new SaleItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }

        sale.Recalculate();
        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);   // assigns SaleNumber-bearing row an Id for the movement reference

        foreach (var item in sale.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            await stockLedger.RecordAsync(
                product,
                DomainEnums.MovementType.Sale,
                -item.Quantity,
                $"Sold on {sale.SaleNumber}",
                sale.SaleNumber,
                ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Sale {Number} completed for {Total}", sale.SaleNumber, sale.Total);
        return await GetAsync(sale.Id, ct);
    }

    public async Task<SaleDto> CancelAsync(int id, CancellationToken ct)
    {
        var sale = await db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Sale", id);

        if (sale.Status == DomainEnums.SaleStatus.Cancelled)
        {
            throw new ConflictException($"{sale.SaleNumber} is already cancelled.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var ids = sale.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

        // Cancelling returns the goods to stock as an explicit, auditable return movement.
        foreach (var item in sale.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null)
            {
                continue;
            }

            await stockLedger.RecordAsync(
                product,
                DomainEnums.MovementType.SaleReturn,
                item.Quantity,
                $"Cancellation of {sale.SaleNumber}",
                sale.SaleNumber,
                ct);
        }

        sale.Status = DomainEnums.SaleStatus.Cancelled;
        sale.Touch();

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(sale.Id, ct);
    }

    private static decimal ValidateRate(double rate)
    {
        var value = rate.ToMoney();
        if (value is < 0 or > 100)
        {
            throw new ValidationException("Tax rate must be between 0 and 100.");
        }

        return value;
    }
}
