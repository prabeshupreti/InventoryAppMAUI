using InventoryApp.Contracts.Auth;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Reporting;
using InventoryApp.Contracts.Stock;
using InventoryApp.Contracts.Trade;

namespace InventoryApp.Application.Abstractions;

// The application layer speaks in the generated contract messages rather than in a second,
// near-identical set of DTOs. That keeps one canonical shape for the API surface and removes
// an entire mapping layer, while domain entities still never leave the server.

public interface IAuthApplicationService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct);
    Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<ListUsersResponse> ListUsersAsync(PageRequest request, CancellationToken ct);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct);
    Task<OperationResult> DeleteUserAsync(int id, CancellationToken ct);
}

public interface ICategoryApplicationService
{
    Task<ListCategoriesResponse> ListAsync(ListCategoriesRequest request, CancellationToken ct);
    Task<CategoryDto> GetAsync(int id, CancellationToken ct);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct);
    Task<CategoryDto> UpdateAsync(UpdateCategoryRequest request, CancellationToken ct);
    Task<OperationResult> DeleteAsync(int id, CancellationToken ct);
    Task<LookupList> GetLookupAsync(CancellationToken ct);
}

public interface ISupplierApplicationService
{
    Task<ListSuppliersResponse> ListAsync(ListSuppliersRequest request, CancellationToken ct);
    Task<SupplierDto> GetAsync(int id, CancellationToken ct);
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct);
    Task<SupplierDto> UpdateAsync(UpdateSupplierRequest request, CancellationToken ct);
    Task<OperationResult> DeleteAsync(int id, CancellationToken ct);
    Task<LookupList> GetLookupAsync(CancellationToken ct);
}

public interface IProductApplicationService
{
    Task<ListProductsResponse> ListAsync(ListProductsRequest request, CancellationToken ct);
    Task<ProductDto> GetAsync(int id, CancellationToken ct);
    Task<ProductDto> GetBySkuAsync(string sku, CancellationToken ct);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<ProductDto> UpdateAsync(UpdateProductRequest request, CancellationToken ct);
    Task<OperationResult> DeleteAsync(int id, CancellationToken ct);
    Task<LookupList> GetLookupAsync(ProductLookupRequest request, CancellationToken ct);
}

public interface IInventoryApplicationService
{
    Task<ListMovementsResponse> ListMovementsAsync(ListMovementsRequest request, CancellationToken ct);
    Task<StockOperationResponse> ReceiveAsync(ReceiveStockRequest request, CancellationToken ct);
    Task<StockOperationResponse> IssueAsync(IssueStockRequest request, CancellationToken ct);
    Task<StockOperationResponse> AdjustAsync(AdjustStockRequest request, CancellationToken ct);
    Task<StockOperationResponse> TransferAsync(TransferStockRequest request, CancellationToken ct);
    Task<ListMovementsResponse> GetProductHistoryAsync(ProductHistoryRequest request, CancellationToken ct);
    Task<LowStockAlertResponse> GetLowStockAlertsAsync(CancellationToken ct);
}

public interface IPurchaseApplicationService
{
    Task<ListPurchasesResponse> ListAsync(ListPurchasesRequest request, CancellationToken ct);
    Task<PurchaseOrderDto> GetAsync(int id, CancellationToken ct);
    Task<PurchaseOrderDto> CreateAsync(SavePurchaseRequest request, CancellationToken ct);
    Task<PurchaseOrderDto> UpdateAsync(SavePurchaseRequest request, CancellationToken ct);
    Task<PurchaseOrderDto> SubmitAsync(int id, CancellationToken ct);
    Task<PurchaseOrderDto> ReceiveAsync(ReceivePurchaseRequest request, CancellationToken ct);
    Task<PurchaseOrderDto> CancelAsync(int id, CancellationToken ct);
    Task<OperationResult> DeleteAsync(int id, CancellationToken ct);
}

public interface ISalesApplicationService
{
    Task<ListSalesResponse> ListAsync(ListSalesRequest request, CancellationToken ct);
    Task<SaleDto> GetAsync(int id, CancellationToken ct);
    Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken ct);
    Task<SaleDto> CancelAsync(int id, CancellationToken ct);
}

public interface IReportApplicationService
{
    Task<DashboardResponse> GetDashboardAsync(CancellationToken ct);
    Task<InventoryValuationResponse> GetInventoryValuationAsync(InventoryReportRequest request, CancellationToken ct);
    Task<TradeReportResponse> GetPurchaseReportAsync(TradeReportRequest request, CancellationToken ct);
    Task<TradeReportResponse> GetSalesReportAsync(TradeReportRequest request, CancellationToken ct);
    Task<CsvExport> ExportCsvAsync(ExportRequest request, CancellationToken ct);
}

/// <summary>Applies a stock change and writes the matching audit row. Shared by manual, purchase and sales flows.</summary>
public interface IStockLedger
{
    Task<Domain.Entities.StockMovement> RecordAsync(
        Domain.Entities.Product product,
        Domain.Enums.MovementType type,
        int signedQuantity,
        string reason,
        string reference,
        CancellationToken ct);
}
