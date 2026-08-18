namespace InventoryApp.Domain.Enums;

/// <summary>
/// Values mirror the protobuf enums so mapping is a straight numeric cast.
/// Keep them in sync when editing the .proto files.
/// </summary>
public enum UserRole
{
    Unspecified = 0,
    Administrator = 1,
    InventoryManager = 2,
    Staff = 3
}

public enum StockStatus
{
    Unspecified = 0,
    InStock = 1,
    LowStock = 2,
    OutOfStock = 3
}

public enum MovementType
{
    Unspecified = 0,
    StockIn = 1,
    StockOut = 2,
    Adjustment = 3,
    PurchaseReceipt = 4,
    Sale = 5,
    SaleReturn = 6,
    Transfer = 7
}

public enum PurchaseStatus
{
    Unspecified = 0,
    Draft = 1,
    Ordered = 2,
    Received = 3,
    Cancelled = 4
}

public enum SaleStatus
{
    Unspecified = 0,
    Completed = 1,
    Cancelled = 2
}
