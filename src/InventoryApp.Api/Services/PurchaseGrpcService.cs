using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using InventoryApp.Contracts.Trade;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class PurchaseGrpcService(IPurchaseApplicationService service)
    : PurchaseService.PurchaseServiceBase
{
    [Authorize(Policy = Permissions.ViewPurchases)]
    public override Task<ListPurchasesResponse> ListPurchases(ListPurchasesRequest request, ServerCallContext context) =>
        service.ListAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewPurchases)]
    public override Task<PurchaseOrderDto> GetPurchase(IdRequest request, ServerCallContext context) =>
        service.GetAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ManagePurchases)]
    public override Task<PurchaseOrderDto> CreatePurchase(SavePurchaseRequest request, ServerCallContext context) =>
        service.CreateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManagePurchases)]
    public override Task<PurchaseOrderDto> UpdatePurchase(SavePurchaseRequest request, ServerCallContext context) =>
        service.UpdateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManagePurchases)]
    public override Task<PurchaseOrderDto> SubmitPurchase(IdRequest request, ServerCallContext context) =>
        service.SubmitAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ReceivePurchases)]
    public override Task<PurchaseOrderDto> ReceivePurchase(ReceivePurchaseRequest request, ServerCallContext context) =>
        service.ReceiveAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManagePurchases)]
    public override Task<PurchaseOrderDto> CancelPurchase(IdRequest request, ServerCallContext context) =>
        service.CancelAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ManagePurchases)]
    public override Task<OperationResult> DeletePurchase(IdRequest request, ServerCallContext context) =>
        service.DeleteAsync(request.Id, context.CancellationToken);
}
