using System.Globalization;
using System.Text;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Reporting;
using InventoryApp.Contracts.Stock;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainEnums = InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

public sealed class ReportApplicationService(IInventoryDbContext db) : IReportApplicationService
{
    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        var products = db.Products.AsNoTracking();

        var totals = await products
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Units = g.Sum(p => p.CurrentStock),
                StockValue = g.Sum(p => p.CurrentStock * p.CostPrice),
                RetailValue = g.Sum(p => p.CurrentStock * p.UnitPrice),
                LowStock = g.Count(p => p.IsActive && p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock),
                OutOfStock = g.Count(p => p.IsActive && p.CurrentStock <= 0)
            })
            .FirstOrDefaultAsync(ct);

        var response = new DashboardResponse
        {
            TotalProducts = totals?.Count ?? 0,
            TotalUnitsInStock = totals?.Units ?? 0,
            TotalInventoryValue = (totals?.StockValue ?? 0m).ToWire(),
            TotalRetailValue = (totals?.RetailValue ?? 0m).ToWire(),
            LowStockCount = totals?.LowStock ?? 0,
            OutOfStockCount = totals?.OutOfStock ?? 0,
            TotalCategories = await db.Categories.CountAsync(ct),
            TotalSuppliers = await db.Suppliers.CountAsync(ct)
        };

        response.PurchasesLast30Days = (await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Status == DomainEnums.PurchaseStatus.Received && o.OrderDateUtc >= since)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m).ToWire();

        response.SalesLast30Days = (await db.Sales.AsNoTracking()
            .Where(s => s.Status == DomainEnums.SaleStatus.Completed && s.SaleDateUtc >= since)
            .SumAsync(s => (decimal?)s.Total, ct) ?? 0m).ToWire();

        var recentlyAdded = await products
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(5)
            .ToListAsync(ct);
        response.RecentlyAdded.AddRange(recentlyAdded.Select(ToDashboardProduct));

        var lowStock = await products
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.CurrentStock)
            .Take(5)
            .ToListAsync(ct);
        response.LowStockProducts.AddRange(lowStock.Select(ToDashboardProduct));

        var movements = await db.StockMovements.AsNoTracking()
            .Include(m => m.Product)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(8)
            .ToListAsync(ct);
        response.RecentMovements.AddRange(movements.Select(m => m.ToDto()));

        var recentPurchases = await db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Supplier)
            .OrderByDescending(o => o.OrderDateUtc)
            .Take(5)
            .ToListAsync(ct);

        response.RecentPurchases.AddRange(recentPurchases.Select(o => new DashboardDocument
        {
            Id = o.Id,
            Number = o.OrderNumber,
            Party = o.Supplier?.CompanyName ?? string.Empty,
            Status = o.Status.ToString(),
            Total = o.Total.ToWire(),
            DateUtc = o.OrderDateUtc.ToIso()
        }));

        var recentSales = await db.Sales.AsNoTracking()
            .OrderByDescending(s => s.SaleDateUtc)
            .Take(5)
            .ToListAsync(ct);

        response.RecentSales.AddRange(recentSales.Select(s => new DashboardDocument
        {
            Id = s.Id,
            Number = s.SaleNumber,
            Party = s.CustomerName,
            Status = s.Status.ToString(),
            Total = s.Total.ToWire(),
            DateUtc = s.SaleDateUtc.ToIso()
        }));

        var categoryTotals = await db.Categories.AsNoTracking()
            .Select(c => new
            {
                c.Name,
                Value = c.Products.Sum(p => (decimal?)(p.CurrentStock * p.CostPrice)) ?? 0m,
                Count = c.Products.Count()
            })
            .OrderByDescending(c => c.Value)
            .Take(8)
            .ToListAsync(ct);

        response.ValueByCategory.AddRange(categoryTotals.Select(c => new DashboardSeriesPoint
        {
            Label = c.Name,
            Value = c.Value.ToWire(),
            SecondaryValue = c.Count
        }));

        response.MonthlyTrade.AddRange(await BuildMonthlyTradeAsync(ct));
        return response;
    }

    private static DashboardProduct ToDashboardProduct(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        CurrentStock = product.CurrentStock,
        MinimumStock = product.MinimumStock,
        StockValue = product.StockValue.ToWire(),
        CreatedAtUtc = product.CreatedAtUtc.ToIso()
    };

    private async Task<List<DashboardSeriesPoint>> BuildMonthlyTradeAsync(CancellationToken ct)
    {
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-5);

        var purchases = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.OrderDateUtc >= start && o.Status == DomainEnums.PurchaseStatus.Received)
            .GroupBy(o => new { o.OrderDateUtc.Year, o.OrderDateUtc.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(o => o.Total) })
            .ToListAsync(ct);

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.SaleDateUtc >= start && s.Status == DomainEnums.SaleStatus.Completed)
            .GroupBy(s => new { s.SaleDateUtc.Year, s.SaleDateUtc.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(s => s.Total) })
            .ToListAsync(ct);

        var points = new List<DashboardSeriesPoint>();
        for (var i = 0; i < 6; i++)
        {
            var month = start.AddMonths(i);
            var purchase = purchases.FirstOrDefault(p => p.Year == month.Year && p.Month == month.Month)?.Total ?? 0m;
            var sale = sales.FirstOrDefault(s => s.Year == month.Year && s.Month == month.Month)?.Total ?? 0m;

            points.Add(new DashboardSeriesPoint
            {
                Label = month.ToString("MMM", CultureInfo.InvariantCulture),
                Value = purchase.ToWire(),
                SecondaryValue = sale.ToWire()
            });
        }

        return points;
    }

    public async Task<InventoryValuationResponse> GetInventoryValuationAsync(
        InventoryReportRequest request, CancellationToken ct)
    {
        var rows = await BuildInventoryRowsAsync(
            request.ReportType,
            request.HasCategoryId ? request.CategoryId : null,
            request.HasSupplierId ? request.SupplierId : null,
            request.Search,
            ct);

        var response = new InventoryValuationResponse
        {
            TotalProducts = rows.Count,
            TotalUnits = rows.Sum(r => r.CurrentStock),
            TotalStockValue = rows.Sum(r => r.StockValue),
            TotalRetailValue = rows.Sum(r => r.RetailValue)
        };

        response.Rows.AddRange(rows);
        return response;
    }

    private async Task<List<InventoryValuationRow>> BuildInventoryRowsAsync(
        ReportType type, int? categoryId, int? supplierId, string? search, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsQueryable();

        query = type switch
        {
            ReportType.LowStock => query.Where(p => p.IsActive && p.CurrentStock > 0 && p.CurrentStock <= p.MinimumStock),
            ReportType.OutOfStock => query.Where(p => p.IsActive && p.CurrentStock <= 0),
            ReportType.InventoryValuation => query.Where(p => p.CurrentStock > 0),
            _ => query
        };

        if (categoryId is > 0) query = query.Where(p => p.CategoryId == categoryId);
        if (supplierId is > 0) query = query.Where(p => p.SupplierId == supplierId);

        var term = (search ?? string.Empty).Trim();
        if (term.Length > 0)
        {
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Sku, $"%{term}%"));
        }

        var products = await query
            .OrderBy(p => p.Name)
            .Take(2000)   // reports are bounded; use CSV export for anything larger
            .ToListAsync(ct);

        var ids = products.Select(p => p.Id).ToList();

        var lastMovements = await db.StockMovements.AsNoTracking()
            .Where(m => ids.Contains(m.ProductId))
            .GroupBy(m => m.ProductId)
            .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First())
            .ToListAsync(ct);

        return products.Select(p =>
        {
            var last = lastMovements.FirstOrDefault(m => m.ProductId == p.Id);
            return new InventoryValuationRow
            {
                ProductId = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                CategoryName = p.Category?.Name ?? string.Empty,
                SupplierName = p.Supplier?.CompanyName ?? string.Empty,
                CurrentStock = p.CurrentStock,
                MinimumStock = p.MinimumStock,
                CostPrice = p.CostPrice.ToWire(),
                UnitPrice = p.UnitPrice.ToWire(),
                StockValue = (p.CurrentStock * p.CostPrice).ToWire(),
                RetailValue = (p.CurrentStock * p.UnitPrice).ToWire(),
                LastMovementType = last is null ? MovementType.Unspecified : (MovementType)(int)last.MovementType,
                LastMovementAtUtc = last?.CreatedAtUtc.ToIso()
            };
        }).ToList();
    }

    public async Task<TradeReportResponse> GetPurchaseReportAsync(TradeReportRequest request, CancellationToken ct)
    {
        var rows = await BuildPurchaseRowsAsync(request, ct);
        return BuildTradeResponse(rows);
    }

    public async Task<TradeReportResponse> GetSalesReportAsync(TradeReportRequest request, CancellationToken ct)
    {
        var rows = await BuildSalesRowsAsync(request, ct);
        return BuildTradeResponse(rows);
    }

    private static TradeReportResponse BuildTradeResponse(List<TradeReportRow> rows)
    {
        var response = new TradeReportResponse
        {
            DocumentCount = rows.Count,
            TotalQuantity = rows.Sum(r => r.TotalQuantity),
            TotalAmount = rows.Sum(r => r.TotalAmount)
        };

        response.Rows.AddRange(rows);
        return response;
    }

    private async Task<List<TradeReportRow>> BuildPurchaseRowsAsync(TradeReportRequest request, CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Items)
            .AsQueryable();

        var (from, to) = TimeConversion.Resolve(request.DateRange);
        if (from.HasValue) query = query.Where(o => o.OrderDateUtc >= from.Value);
        if (to.HasValue) query = query.Where(o => o.OrderDateUtc <= to.Value);
        if (request.HasSupplierId && request.SupplierId > 0) query = query.Where(o => o.SupplierId == request.SupplierId);
        if (request.HasProductId && request.ProductId > 0) query = query.Where(o => o.Items.Any(i => i.ProductId == request.ProductId));
        if (request.HasCategoryId && request.CategoryId > 0) query = query.Where(o => o.Items.Any(i => i.Product!.CategoryId == request.CategoryId));

        var orders = await query
            .OrderByDescending(o => o.OrderDateUtc)
            .Take(2000)
            .ToListAsync(ct);

        return orders.Select(o => new TradeReportRow
        {
            DocumentNumber = o.OrderNumber,
            DateUtc = o.OrderDateUtc.ToIso(),
            Party = o.Supplier?.CompanyName ?? string.Empty,
            Status = o.Status.ToString(),
            LineCount = o.Items.Count,
            TotalQuantity = o.Items.Sum(i => i.Quantity),
            TotalAmount = o.Total.ToWire()
        }).ToList();
    }

    private async Task<List<TradeReportRow>> BuildSalesRowsAsync(TradeReportRequest request, CancellationToken ct)
    {
        var query = db.Sales.AsNoTracking().Include(s => s.Items).AsQueryable();

        var (from, to) = TimeConversion.Resolve(request.DateRange);
        if (from.HasValue) query = query.Where(s => s.SaleDateUtc >= from.Value);
        if (to.HasValue) query = query.Where(s => s.SaleDateUtc <= to.Value);
        if (request.HasProductId && request.ProductId > 0) query = query.Where(s => s.Items.Any(i => i.ProductId == request.ProductId));
        if (request.HasCategoryId && request.CategoryId > 0) query = query.Where(s => s.Items.Any(i => i.Product!.CategoryId == request.CategoryId));

        var sales = await query
            .OrderByDescending(s => s.SaleDateUtc)
            .Take(2000)
            .ToListAsync(ct);

        return sales.Select(s => new TradeReportRow
        {
            DocumentNumber = s.SaleNumber,
            DateUtc = s.SaleDateUtc.ToIso(),
            Party = s.CustomerName,
            Status = s.Status.ToString(),
            LineCount = s.Items.Count,
            TotalQuantity = s.Items.Sum(i => i.Quantity),
            TotalAmount = s.Total.ToWire()
        }).ToList();
    }

    public async Task<CsvExport> ExportCsvAsync(ExportRequest request, CancellationToken ct)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);

        return request.ReportType switch
        {
            ReportType.StockMovements => await ExportMovementsAsync(request, stamp, ct),
            ReportType.Purchases => await ExportTradeAsync(
                await BuildPurchaseRowsAsync(ToTradeRequest(request), ct), "purchases", "Supplier", stamp),
            ReportType.Sales => await ExportTradeAsync(
                await BuildSalesRowsAsync(ToTradeRequest(request), ct), "sales", "Customer", stamp),
            _ => await ExportInventoryAsync(request, stamp, ct)
        };
    }

    private static TradeReportRequest ToTradeRequest(ExportRequest request)
    {
        var trade = new TradeReportRequest { DateRange = request.DateRange };
        if (request.HasSupplierId) trade.SupplierId = request.SupplierId;
        if (request.HasCategoryId) trade.CategoryId = request.CategoryId;
        if (request.HasProductId) trade.ProductId = request.ProductId;
        return trade;
    }

    private async Task<CsvExport> ExportInventoryAsync(ExportRequest request, string stamp, CancellationToken ct)
    {
        var rows = await BuildInventoryRowsAsync(
            request.ReportType,
            request.HasCategoryId ? request.CategoryId : null,
            request.HasSupplierId ? request.SupplierId : null,
            request.Search,
            ct);

        var builder = new StringBuilder();
        builder.AppendLine("SKU,Product,Category,Supplier,Current Stock,Minimum Stock,Cost Price,Unit Price,Stock Value,Retail Value,Last Movement");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Csv(row.Sku), Csv(row.Name), Csv(row.CategoryName), Csv(row.SupplierName),
                row.CurrentStock, row.MinimumStock,
                Num(row.CostPrice), Num(row.UnitPrice), Num(row.StockValue), Num(row.RetailValue),
                Csv(row.HasLastMovementAtUtc ? row.LastMovementAtUtc : string.Empty)));
        }

        var name = request.ReportType.ToString().ToLowerInvariant();
        return new CsvExport
        {
            FileName = $"{name}-{stamp}.csv",
            Content = builder.ToString(),
            RowCount = rows.Count
        };
    }

    private async Task<CsvExport> ExportMovementsAsync(ExportRequest request, string stamp, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking().Include(m => m.Product).AsQueryable();

        var (from, to) = TimeConversion.Resolve(request.DateRange);
        if (from.HasValue) query = query.Where(m => m.CreatedAtUtc >= from.Value);
        if (to.HasValue) query = query.Where(m => m.CreatedAtUtc <= to.Value);
        if (request.HasProductId && request.ProductId > 0) query = query.Where(m => m.ProductId == request.ProductId);
        if (request.HasCategoryId && request.CategoryId > 0) query = query.Where(m => m.Product!.CategoryId == request.CategoryId);

        if (request.HasMovementType && request.MovementType != MovementType.Unspecified)
        {
            var type = request.MovementType.ToDomain();
            query = query.Where(m => m.MovementType == type);
        }

        var movements = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(10000)
            .ToListAsync(ct);

        var builder = new StringBuilder();
        builder.AppendLine("Date,SKU,Product,Type,Quantity,Previous,New,Reason,Reference,User");

        foreach (var m in movements)
        {
            builder.AppendLine(string.Join(',',
                Csv(m.CreatedAtUtc.ToIso()), Csv(m.Product?.Sku), Csv(m.Product?.Name),
                Csv(m.MovementType.ToString()), m.Quantity, m.PreviousQuantity, m.NewQuantity,
                Csv(m.Reason), Csv(m.Reference), Csv(m.UserName)));
        }

        return new CsvExport
        {
            FileName = $"stock-movements-{stamp}.csv",
            Content = builder.ToString(),
            RowCount = movements.Count
        };
    }

    private static Task<CsvExport> ExportTradeAsync(List<TradeReportRow> rows, string name, string partyLabel, string stamp)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Document,Date,{partyLabel},Status,Lines,Quantity,Total");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Csv(row.DocumentNumber), Csv(row.DateUtc), Csv(row.Party), Csv(row.Status),
                row.LineCount, row.TotalQuantity, Num(row.TotalAmount)));
        }

        return Task.FromResult(new CsvExport
        {
            FileName = $"{name}-{stamp}.csv",
            Content = builder.ToString(),
            RowCount = rows.Count
        });
    }

    /// <summary>RFC 4180 quoting so commas and quotes inside names cannot break the file.</summary>
    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    private static string Num(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
