using InventoryApp.Contracts.Auth;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Reporting;
using InventoryApp.Contracts.Stock;
using InventoryApp.Contracts.Trade;

namespace InventoryApp.Client.Services;

/// <summary>
/// The single seam between Blazor components and the backend. Components never touch
/// generated gRPC stubs directly, which keeps retry/caching/offline concerns in one place.
/// </summary>
public sealed class InventoryApiClient(
    GrpcChannelProvider channelProvider,
    GrpcCallExecutor executor,
    LookupCache cache)
{
    private const string CategoryLookupKey = "lookup.categories";
    private const string SupplierLookupKey = "lookup.suppliers";

    private AuthenticationService.AuthenticationServiceClient Auth => new(channelProvider.Invoker);
    private CategoryService.CategoryServiceClient Categories => new(channelProvider.Invoker);
    private SupplierService.SupplierServiceClient Suppliers => new(channelProvider.Invoker);
    private ProductService.ProductServiceClient Products => new(channelProvider.Invoker);
    private InventoryService.InventoryServiceClient Stock => new(channelProvider.Invoker);
    private PurchaseService.PurchaseServiceClient Purchases => new(channelProvider.Invoker);
    private SalesService.SalesServiceClient Sales => new(channelProvider.Invoker);
    private ReportService.ReportServiceClient Reports => new(channelProvider.Invoker);

    // ---------- Authentication ----------

    public Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.LoginAsync(new LoginRequest { Username = username, Password = password },
                cancellationToken: ct).ResponseAsync,
            "sign in");

    public Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.GetCurrentUserAsync(new Empty(), cancellationToken: ct).ResponseAsync,
            "load your profile");

    public Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.ChangePasswordAsync(request, cancellationToken: ct).ResponseAsync,
            "change your password");

    public Task<ListUsersResponse> ListUsersAsync(PageRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.ListUsersAsync(request, cancellationToken: ct).ResponseAsync,
            "load users");

    public Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.CreateUserAsync(request, cancellationToken: ct).ResponseAsync,
            "create the user");

    public Task<UserDto> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.UpdateUserAsync(request, cancellationToken: ct).ResponseAsync,
            "update the user");

    public Task<OperationResult> DeleteUserAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Auth.DeleteUserAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "delete the user");

    // ---------- Categories ----------

    public Task<ListCategoriesResponse> ListCategoriesAsync(ListCategoriesRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Categories.ListCategoriesAsync(request, cancellationToken: ct).ResponseAsync,
            "load categories");

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(
            () => Categories.CreateCategoryAsync(request, cancellationToken: ct).ResponseAsync,
            "create the category");

        cache.Invalidate(CategoryLookupKey);
        return result;
    }

    public async Task<CategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(
            () => Categories.UpdateCategoryAsync(request, cancellationToken: ct).ResponseAsync,
            "update the category");

        cache.Invalidate(CategoryLookupKey);
        return result;
    }

    public async Task<OperationResult> DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(
            () => Categories.DeleteCategoryAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "delete the category");

        cache.Invalidate(CategoryLookupKey);
        return result;
    }

    public Task<LookupList> GetCategoryLookupAsync(CancellationToken ct = default) =>
        cache.GetOrLoadAsync(CategoryLookupKey, () => executor.ExecuteAsync(
            () => Categories.GetCategoryLookupAsync(new Empty(), cancellationToken: ct).ResponseAsync,
            "load categories"));

    // ---------- Suppliers ----------

    public Task<ListSuppliersResponse> ListSuppliersAsync(ListSuppliersRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Suppliers.ListSuppliersAsync(request, cancellationToken: ct).ResponseAsync,
            "load suppliers");

    public Task<SupplierDto> GetSupplierAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Suppliers.GetSupplierAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "load the supplier");

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(
            () => Suppliers.CreateSupplierAsync(request, cancellationToken: ct).ResponseAsync,
            "create the supplier");

        cache.Invalidate(SupplierLookupKey);
        return result;
    }

    public async Task<SupplierDto> UpdateSupplierAsync(UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(
            () => Suppliers.UpdateSupplierAsync(request, cancellationToken: ct).ResponseAsync,
            "update the supplier");

        cache.Invalidate(SupplierLookupKey);
        return result;
    }

    public async Task<OperationResult> DeleteSupplierAsync(int id, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(
            () => Suppliers.DeleteSupplierAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "delete the supplier");

        cache.Invalidate(SupplierLookupKey);
        return result;
    }

    public Task<LookupList> GetSupplierLookupAsync(CancellationToken ct = default) =>
        cache.GetOrLoadAsync(SupplierLookupKey, () => executor.ExecuteAsync(
            () => Suppliers.GetSupplierLookupAsync(new Empty(), cancellationToken: ct).ResponseAsync,
            "load suppliers"));

    // ---------- Products ----------

    public Task<ListProductsResponse> ListProductsAsync(ListProductsRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.ListProductsAsync(request, cancellationToken: ct).ResponseAsync,
            "load products");

    public Task<ProductDto> GetProductAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.GetProductAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "load the product");

    public Task<ProductDto> GetProductBySkuAsync(string sku, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.GetProductBySkuAsync(new GetProductBySkuRequest { Sku = sku },
                cancellationToken: ct).ResponseAsync,
            "find the product");

    public Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.CreateProductAsync(request, cancellationToken: ct).ResponseAsync,
            "create the product");

    public Task<ProductDto> UpdateProductAsync(UpdateProductRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.UpdateProductAsync(request, cancellationToken: ct).ResponseAsync,
            "update the product");

    public Task<OperationResult> DeleteProductAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.DeleteProductAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "delete the product");

    public Task<LookupList> GetProductLookupAsync(string search, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Products.GetProductLookupAsync(
                new ProductLookupRequest { OnlyActive = true, Search = search ?? string.Empty },
                cancellationToken: ct).ResponseAsync,
            "search products");

    // ---------- Stock ----------

    public Task<ListMovementsResponse> ListMovementsAsync(ListMovementsRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.ListMovementsAsync(request, cancellationToken: ct).ResponseAsync,
            "load stock movements");

    public Task<StockOperationResponse> ReceiveStockAsync(ReceiveStockRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.ReceiveStockAsync(request, cancellationToken: ct).ResponseAsync,
            "receive stock");

    public Task<StockOperationResponse> IssueStockAsync(IssueStockRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.IssueStockAsync(request, cancellationToken: ct).ResponseAsync,
            "remove stock");

    public Task<StockOperationResponse> AdjustStockAsync(AdjustStockRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.AdjustStockAsync(request, cancellationToken: ct).ResponseAsync,
            "adjust stock");

    public Task<StockOperationResponse> TransferStockAsync(TransferStockRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.TransferStockAsync(request, cancellationToken: ct).ResponseAsync,
            "transfer stock");

    public Task<ListMovementsResponse> GetProductHistoryAsync(ProductHistoryRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.GetProductHistoryAsync(request, cancellationToken: ct).ResponseAsync,
            "load product history");

    public Task<LowStockAlertResponse> GetLowStockAlertsAsync(CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Stock.GetLowStockAlertsAsync(new Empty(), cancellationToken: ct).ResponseAsync,
            "load low stock alerts");

    // ---------- Purchases ----------

    public Task<ListPurchasesResponse> ListPurchasesAsync(ListPurchasesRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.ListPurchasesAsync(request, cancellationToken: ct).ResponseAsync,
            "load purchase orders");

    public Task<PurchaseOrderDto> GetPurchaseAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.GetPurchaseAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "load the purchase order");

    public Task<PurchaseOrderDto> CreatePurchaseAsync(SavePurchaseRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.CreatePurchaseAsync(request, cancellationToken: ct).ResponseAsync,
            "create the purchase order");

    public Task<PurchaseOrderDto> UpdatePurchaseAsync(SavePurchaseRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.UpdatePurchaseAsync(request, cancellationToken: ct).ResponseAsync,
            "update the purchase order");

    public Task<PurchaseOrderDto> SubmitPurchaseAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.SubmitPurchaseAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "submit the purchase order");

    public Task<PurchaseOrderDto> ReceivePurchaseAsync(int id, string reference, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.ReceivePurchaseAsync(
                new ReceivePurchaseRequest { Id = id, Reference = reference ?? string.Empty },
                cancellationToken: ct).ResponseAsync,
            "receive the purchase order");

    public Task<PurchaseOrderDto> CancelPurchaseAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.CancelPurchaseAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "cancel the purchase order");

    public Task<OperationResult> DeletePurchaseAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Purchases.DeletePurchaseAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "delete the draft order");

    // ---------- Sales ----------

    public Task<ListSalesResponse> ListSalesAsync(ListSalesRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Sales.ListSalesAsync(request, cancellationToken: ct).ResponseAsync,
            "load sales");

    public Task<SaleDto> GetSaleAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Sales.GetSaleAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "load the sale");

    public Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Sales.CreateSaleAsync(request, cancellationToken: ct).ResponseAsync,
            "record the sale");

    public Task<SaleDto> CancelSaleAsync(int id, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Sales.CancelSaleAsync(new IdRequest { Id = id }, cancellationToken: ct).ResponseAsync,
            "cancel the sale");

    // ---------- Reports ----------

    public Task<DashboardResponse> GetDashboardAsync(CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Reports.GetDashboardAsync(new Empty(), cancellationToken: ct).ResponseAsync,
            "load the dashboard");

    public Task<InventoryValuationResponse> GetInventoryReportAsync(
        InventoryReportRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Reports.GetInventoryValuationAsync(request, cancellationToken: ct).ResponseAsync,
            "load the inventory report");

    public Task<TradeReportResponse> GetPurchaseReportAsync(TradeReportRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Reports.GetPurchaseReportAsync(request, cancellationToken: ct).ResponseAsync,
            "load the purchase report");

    public Task<TradeReportResponse> GetSalesReportAsync(TradeReportRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Reports.GetSalesReportAsync(request, cancellationToken: ct).ResponseAsync,
            "load the sales report");

    public Task<CsvExport> ExportCsvAsync(ExportRequest request, CancellationToken ct = default) =>
        executor.ExecuteAsync(
            () => Reports.ExportCsvAsync(request, cancellationToken: ct).ResponseAsync,
            "export the report");

    public void ClearCaches() => cache.Clear();
}
