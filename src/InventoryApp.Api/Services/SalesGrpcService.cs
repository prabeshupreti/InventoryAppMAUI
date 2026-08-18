using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using InventoryApp.Contracts.Trade;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class SalesGrpcService(ISalesApplicationService service)
    : SalesService.SalesServiceBase
{
    [Authorize(Policy = Permissions.ViewSales)]
    public override Task<ListSalesResponse> ListSales(ListSalesRequest request, ServerCallContext context) =>
        service.ListAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewSales)]
    public override Task<SaleDto> GetSale(IdRequest request, ServerCallContext context) =>
        service.GetAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageSales)]
    public override Task<SaleDto> CreateSale(CreateSaleRequest request, ServerCallContext context) =>
        service.CreateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageSales)]
    public override Task<SaleDto> CancelSale(IdRequest request, ServerCallContext context) =>
        service.CancelAsync(request.Id, context.CancellationToken);
}
