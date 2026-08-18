namespace InventoryApp.Contracts.Security;

/// <summary>
/// The single source of truth for authorization in the system.
/// Both the API and the MAUI client register ASP.NET Core authorization policies
/// from this map, so a permission never has to be re-implemented per screen.
/// Adding a finer-grained permission later means adding a constant here and
/// putting it in the right role buckets - nothing else changes.
/// </summary>
public static class Permissions
{
    public const string ViewDashboard = "inventory.dashboard.view";

    public const string ViewProducts = "inventory.products.view";
    public const string ManageProducts = "inventory.products.manage";
    public const string DeleteProducts = "inventory.products.delete";

    public const string ViewCatalog = "inventory.catalog.view";
    public const string ManageCatalog = "inventory.catalog.manage";   // categories + suppliers

    public const string ViewStock = "inventory.stock.view";
    public const string ReceiveStock = "inventory.stock.receive";
    public const string IssueStock = "inventory.stock.issue";
    public const string AdjustStock = "inventory.stock.adjust";

    public const string ViewPurchases = "inventory.purchases.view";
    public const string ManagePurchases = "inventory.purchases.manage";
    public const string ReceivePurchases = "inventory.purchases.receive";

    public const string ViewSales = "inventory.sales.view";
    public const string ManageSales = "inventory.sales.manage";

    public const string ViewReports = "inventory.reports.view";
    public const string ExportReports = "inventory.reports.export";

    public const string ManageUsers = "inventory.users.manage";

    /// <summary>Claim type used to carry permissions inside the JWT.</summary>
    public const string ClaimType = "perm";

    public static readonly IReadOnlyList<string> All =
    [
        ViewDashboard,
        ViewProducts, ManageProducts, DeleteProducts,
        ViewCatalog, ManageCatalog,
        ViewStock, ReceiveStock, IssueStock, AdjustStock,
        ViewPurchases, ManagePurchases, ReceivePurchases,
        ViewSales, ManageSales,
        ViewReports, ExportReports,
        ManageUsers
    ];
}
