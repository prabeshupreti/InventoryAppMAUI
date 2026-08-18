using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryApp.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add ...` work against this project without booting the API host.
/// Run from the repository root:
///   dotnet ef migrations add InitialCreate -p src/InventoryApp.Infrastructure -s src/InventoryApp.Api
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<InventoryDbContext>();
        builder.UseSqlite("Data Source=inventory.db");
        return new InventoryDbContext(builder.Options);
    }
}
