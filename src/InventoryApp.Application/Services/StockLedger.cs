using InventoryApp.Application.Abstractions;
using InventoryApp.Domain.Entities;
using InventoryApp.Domain.Enums;

namespace InventoryApp.Application.Services;

/// <summary>
/// The single place where product quantity changes. Every caller gets an audit row for free,
/// which is what makes the movement history trustworthy.
/// Callers are responsible for SaveChangesAsync so the change can join a wider transaction.
/// </summary>
public sealed class StockLedger(IInventoryDbContext db, ICurrentUser currentUser) : IStockLedger
{
    public Task<StockMovement> RecordAsync(
        Product product,
        MovementType type,
        int signedQuantity,
        string reason,
        string reference,
        CancellationToken ct)
    {
        var previous = product.ApplyStockDelta(signedQuantity);

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Product = product,
            MovementType = type,
            Quantity = signedQuantity,
            PreviousQuantity = previous,
            NewQuantity = product.CurrentStock,
            Reason = reason,
            Reference = reference,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            FromLocation = product.Location,
            ToLocation = product.Location,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.StockMovements.Add(movement);
        return Task.FromResult(movement);
    }
}
