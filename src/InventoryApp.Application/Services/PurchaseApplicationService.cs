using System.Linq.Expressions;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Trade;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

public sealed class PurchaseApplicationService(
    IInventoryDbContext db,
    IStockLedger stockLedger,
    IDocumentNumberGenerator numbers,
    ICurrentUser currentUser,
    ILogger<PurchaseApplicationService> logger) : IPurchaseApplicationService
{
    private static readonly Dictionary<string, Expression<Func<PurchaseOrder, object>>> SortMap = new()
    {
        ["orderNumber"] = o => o.OrderNumber,
        ["supplierName"] = o => o.Supplier!.CompanyName,
        ["orderDateUtc"] = o => o.OrderDateUtc,
        ["status"] = o => o.Status,
        ["total"] = o => o.Total
    };

    public async Task<ListPurchasesResponse> ListAsync(ListPurchasesRequest request, CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        var search = Paging.SearchTerm(request.Page);
        if (search.Length > 0)
        {
            query = query.Where(o =>
                EF.Functions.Like(o.OrderNumber, $"%{search}%") ||
                EF.Functions.Like(o.Supplier!.CompanyName, $"%{search}%") ||
                EF.Functions.Like(o.Notes, $"%{search}%"));
        }

        if (request.HasSupplierId && request.SupplierId > 0)
        {
            query = query.Where(o => o.SupplierId == request.SupplierId);
        }

        if (request.HasStatus && request.Status != PurchaseStatus.Unspecified)
        {
            var status = request.Status.ToDomain();
            query = query.Where(o => o.Status == status);
        }

        var (from, to) = TimeConversion.Resolve(request.DateRange);
        if (from.HasValue) query = query.Where(o => o.OrderDateUtc >= from.Value);
        if (to.HasValue) query = query.Where(o => o.OrderDateUtc <= to.Value);

        query = string.IsNullOrWhiteSpace(request.Page?.SortBy)
            ? query.OrderByDescending(o => o.OrderDateUtc).ThenByDescending(o => o.Id)
            : Paging.ApplySort(query, request.Page, SortMap, "orderDateUtc");

        var (items, info) = await Paging.ToPageAsync(query, request.Page, ct);

        var response = new ListPurchasesResponse { PageInfo = info };
        response.Items.AddRange(items.Select(o => o.ToDto()));
        return response;
    }

    public async Task<PurchaseOrderDto> GetAsync(int id, CancellationToken ct)
    {
        var order = await LoadAsync(id, tracking: false, ct);
        return order.ToDto();
    }

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseRequest request, CancellationToken ct)
    {
        await ValidateSupplierAsync(request.SupplierId, ct);
        var lines = await ValidateItemsAsync(request.Items, ct);

        var order = new PurchaseOrder
        {
            OrderNumber = await numbers.NextPurchaseNumberAsync(ct),
            SupplierId = request.SupplierId,
            OrderDateUtc = TimeConversion.ParseUtc(request.OrderDateUtc, DateTime.UtcNow),
            ExpectedDateUtc = TimeConversion.ParseUtcOrNull(request.HasExpectedDateUtc ? request.ExpectedDateUtc : null),
            Status = DomainEnums.PurchaseStatus.Draft,
            TaxRate = ValidateRate(request.TaxRate),
            DiscountAmount = request.DiscountAmount.ToMoney(),
            Notes = Guard.Optional(request.Notes, "Notes", 1000),
            CreatedByUserId = currentUser.UserId,
            CreatedByName = currentUser.UserName
        };

        foreach (var (productId, quantity, unitCost) in lines)
        {
            order.Items.Add(new PurchaseOrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitCost = unitCost
            });
        }

        order.Recalculate();
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Purchase order {Number} created", order.OrderNumber);
        return await GetAsync(order.Id, ct);
    }

    public async Task<PurchaseOrderDto> UpdateAsync(SavePurchaseRequest request, CancellationToken ct)
    {
        if (!request.HasId || request.Id <= 0)
        {
            throw new ValidationException("A purchase order id is required for an update.");
        }

        var order = await LoadAsync(request.Id, tracking: true, ct);

        if (!order.IsEditable)
        {
            throw new ConflictException(
                $"Purchase order {order.OrderNumber} is {order.Status.ToString().ToLowerInvariant()} and can no longer be edited.");
        }

        await ValidateSupplierAsync(request.SupplierId, ct);
        var lines = await ValidateItemsAsync(request.Items, ct);

        order.SupplierId = request.SupplierId;
        order.OrderDateUtc = TimeConversion.ParseUtc(request.OrderDateUtc, order.OrderDateUtc);
        order.ExpectedDateUtc = TimeConversion.ParseUtcOrNull(request.HasExpectedDateUtc ? request.ExpectedDateUtc : null);
        order.TaxRate = ValidateRate(request.TaxRate);
        order.DiscountAmount = request.DiscountAmount.ToMoney();
        order.Notes = Guard.Optional(request.Notes, "Notes", 1000);

        // Draft lines are fully replaced; simpler and safer than diffing for a document that has no side effects yet.
        db.PurchaseOrderItems.RemoveRange(order.Items);
        order.Items.Clear();

        foreach (var (productId, quantity, unitCost) in lines)
        {
            order.Items.Add(new PurchaseOrderItem
            {
                PurchaseOrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitCost = unitCost
            });
        }

        order.Recalculate();
        order.Touch();
        await db.SaveChangesAsync(ct);

        return await GetAsync(order.Id, ct);
    }

    public async Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct)
    {
        var order = await LoadAsync(id, tracking: true, ct);

        if (order.Status != DomainEnums.PurchaseStatus.Draft)
        {
            throw new ConflictException($"Only draft orders can be submitted. {order.OrderNumber} is {order.Status}.");
        }

        if (order.Items.Count == 0)
        {
            throw new ValidationException("Add at least one product before submitting the order.");
        }

        order.Status = DomainEnums.PurchaseStatus.Ordered;
        order.Touch();
        await db.SaveChangesAsync(ct);

        return await GetAsync(order.Id, ct);
    }

    public async Task<PurchaseOrderDto> ReceiveAsync(ReceivePurchaseRequest request, CancellationToken ct)
    {
        var order = await LoadAsync(request.Id, tracking: true, ct);

        if (!order.CanBeReceived)
        {
            throw new ConflictException(
                $"Purchase order {order.OrderNumber} is already {order.Status.ToString().ToLowerInvariant()}.");
        }

        if (order.Items.Count == 0)
        {
            throw new ValidationException("This order has no lines to receive.");
        }

        // Receiving is the moment stock actually increases; each line writes its own audit row.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var reference = Guard.Optional(request.Reference, "Reference", 100);
        if (reference.Length == 0)
        {
            reference = order.OrderNumber;
        }

        foreach (var item in order.Items)
        {
            var product = item.Product ?? throw new ValidationException("A product on this order no longer exists.");

            await stockLedger.RecordAsync(
                product,
                DomainEnums.MovementType.PurchaseReceipt,
                item.Quantity,
                $"Received against {order.OrderNumber}",
                reference,
                ct);

            // Keep the moving cost price aligned with the latest purchase price.
            if (item.UnitCost > 0)
            {
                product.CostPrice = item.UnitCost;
            }
        }

        order.Status = DomainEnums.PurchaseStatus.Received;
        order.ReceivedDateUtc = DateTime.UtcNow;
        order.Touch();

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Purchase order {Number} received by {User}", order.OrderNumber, currentUser.UserName);
        return await GetAsync(order.Id, ct);
    }

    public async Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct)
    {
        var order = await LoadAsync(id, tracking: true, ct);

        if (order.Status == DomainEnums.PurchaseStatus.Received)
        {
            throw new ConflictException(
                $"{order.OrderNumber} has already been received. Use a stock removal to reverse the goods.");
        }

        if (order.Status == DomainEnums.PurchaseStatus.Cancelled)
        {
            throw new ConflictException($"{order.OrderNumber} is already cancelled.");
        }

        order.Status = DomainEnums.PurchaseStatus.Cancelled;
        order.Touch();
        await db.SaveChangesAsync(ct);

        return await GetAsync(order.Id, ct);
    }

    public async Task<OperationResult> DeleteAsync(int id, CancellationToken ct)
    {
        var order = await LoadAsync(id, tracking: true, ct);

        if (order.Status != DomainEnums.PurchaseStatus.Draft)
        {
            throw new ConflictException(
                "Only draft orders can be deleted. Cancel the order instead so the history is preserved.");
        }

        db.PurchaseOrders.Remove(order);
        await db.SaveChangesAsync(ct);

        return new OperationResult { Success = true, Message = $"Draft {order.OrderNumber} deleted." };
    }

    private async Task<PurchaseOrder> LoadAsync(int id, bool tracking, CancellationToken ct)
    {
        var query = db.PurchaseOrders
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(o => o.Id == id, ct)
               ?? throw new NotFoundException("Purchase order", id);
    }

    private async Task ValidateSupplierAsync(int supplierId, CancellationToken ct)
    {
        Guard.Positive(supplierId, "Supplier");

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId, ct)
                       ?? throw new ValidationException("The selected supplier no longer exists.");

        if (!supplier.IsActive)
        {
            throw new ValidationException($"'{supplier.CompanyName}' is inactive and cannot receive new orders.");
        }
    }

    private async Task<List<(int ProductId, int Quantity, decimal UnitCost)>> ValidateItemsAsync(
        IEnumerable<SavePurchaseItem> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new ValidationException("Add at least one product to the order.");
        }

        // Merge duplicate lines for the same product rather than rejecting them.
        var merged = list
            .GroupBy(i => i.ProductId)
            .Select(g => (
                ProductId: g.Key,
                Quantity: g.Sum(x => x.Quantity),
                UnitCost: g.Last().UnitCost.ToMoney()))
            .ToList();

        foreach (var line in merged)
        {
            Guard.Positive(line.ProductId, "Product");
            Guard.Positive(line.Quantity, "Quantity");
            Guard.NotNegative(line.UnitCost, "Unit cost");
        }

        var ids = merged.Select(m => m.ProductId).ToList();
        var found = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.IsActive, p.Name })
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            var product = found.FirstOrDefault(p => p.Id == id)
                          ?? throw new ValidationException($"Product {id} no longer exists.");

            if (!product.IsActive)
            {
                throw new ValidationException($"'{product.Name}' is inactive and cannot be ordered.");
            }
        }

        return merged;
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
