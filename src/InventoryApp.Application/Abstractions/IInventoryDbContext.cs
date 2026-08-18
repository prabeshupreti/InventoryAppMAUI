using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InventoryApp.Application.Abstractions;

/// <summary>
/// Abstraction over the persistence context so the application layer does not depend on
/// the Infrastructure project. Implemented by InventoryDbContext.
/// </summary>
public interface IInventoryDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Product> Products { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
