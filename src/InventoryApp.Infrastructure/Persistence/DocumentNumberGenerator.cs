using InventoryApp.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// Produces sequential per-year document numbers (PO-2025-0001, SO-2025-0001).
/// Wrapped in a semaphore so two concurrent requests in the same process cannot collide;
/// the unique index on the number column is the ultimate guard.
/// </summary>
public sealed class DocumentNumberGenerator(InventoryDbContext db) : IDocumentNumberGenerator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public Task<string> NextPurchaseNumberAsync(CancellationToken ct) =>
        NextAsync("PO", ct);

    public Task<string> NextSaleNumberAsync(CancellationToken ct) =>
        NextAsync("SO", ct);

    private async Task<string> NextAsync(string prefix, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var year = DateTime.UtcNow.Year;
            var pattern = $"{prefix}-{year}-";

            var count = prefix == "PO"
                ? await db.PurchaseOrders.CountAsync(o => o.OrderNumber.StartsWith(pattern), ct)
                : await db.Sales.CountAsync(s => s.SaleNumber.StartsWith(pattern), ct);

            return $"{pattern}{count + 1:D4}";
        }
        finally
        {
            Gate.Release();
        }
    }
}
