using InventoryApp.Contracts.Auth;

namespace InventoryApp.Contracts.Security;

/// <summary>Maps a role onto the concrete permissions it grants.</summary>
public static class RolePermissions
{
    public const string RoleAdministrator = "Administrator";
    public const string RoleInventoryManager = "InventoryManager";
    public const string RoleStaff = "Staff";

    private static readonly string[] StaffPermissions =
    [
        Permissions.ViewDashboard,
        Permissions.ViewProducts,
        Permissions.ViewCatalog,
        Permissions.ViewStock,
        Permissions.ViewPurchases,
        Permissions.ViewSales,
        Permissions.ManageSales,
        Permissions.ViewReports
    ];

    private static readonly string[] InventoryManagerPermissions =
    [
        .. StaffPermissions,
        Permissions.ManageProducts,
        Permissions.ManageCatalog,
        Permissions.ReceiveStock,
        Permissions.IssueStock,
        Permissions.AdjustStock,
        Permissions.ManagePurchases,
        Permissions.ReceivePurchases,
        Permissions.ExportReports
    ];

    public static string ToRoleName(UserRole role) => role switch
    {
        UserRole.Administrator => RoleAdministrator,
        UserRole.InventoryManager => RoleInventoryManager,
        UserRole.Staff => RoleStaff,
        _ => RoleStaff
    };

    public static UserRole FromRoleName(string? roleName) => roleName switch
    {
        RoleAdministrator => UserRole.Administrator,
        RoleInventoryManager => UserRole.InventoryManager,
        RoleStaff => UserRole.Staff,
        _ => UserRole.Unspecified
    };

    public static IReadOnlyList<string> For(UserRole role) => role switch
    {
        UserRole.Administrator => Permissions.All,
        UserRole.InventoryManager => InventoryManagerPermissions,
        UserRole.Staff => StaffPermissions,
        _ => []
    };
}
