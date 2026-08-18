using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using InventoryApp.Contracts.Stock;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class InventoryGrpcService(IInventoryApplicationService service)
    : InventoryService.InventoryServiceBase
{
    [Authorize(Policy = Permissions.ViewStock)]
    public override Task<ListMovementsResponse> ListMovements(ListMovementsRequest request, ServerCallContext context) =>
        service.ListMovementsAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ReceiveStock)]
    public override Task<StockOperationResponse> ReceiveStock(ReceiveStockRequest request, ServerCallContext context) =>
        service.ReceiveAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.IssueStock)]
    public override Task<StockOperationResponse> IssueStock(IssueStockRequest request, ServerCallContext context) =>
        service.IssueAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.AdjustStock)]
    public override Task<StockOperationResponse> AdjustStock(AdjustStockRequest request, ServerCallContext context) =>
        service.AdjustAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.AdjustStock)]
    public override Task<StockOperationResponse> TransferStock(TransferStockRequest request, ServerCallContext context) =>
        service.TransferAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewStock)]
    public override Task<ListMovementsResponse> GetProductHistory(ProductHistoryRequest request, ServerCallContext context) =>
        service.GetProductHistoryAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewStock)]
    public override Task<LowStockAlertResponse> GetLowStockAlerts(Empty request, ServerCallContext context) =>
        service.GetLowStockAlertsAsync(context.CancellationToken);
}
