using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class SupplierGrpcService(ISupplierApplicationService service)
    : SupplierService.SupplierServiceBase
{
    [Authorize(Policy = Permissions.ViewCatalog)]
    public override Task<ListSuppliersResponse> ListSuppliers(ListSuppliersRequest request, ServerCallContext context) =>
        service.ListAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewCatalog)]
    public override Task<SupplierDto> GetSupplier(IdRequest request, ServerCallContext context) =>
        service.GetAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageCatalog)]
    public override Task<SupplierDto> CreateSupplier(CreateSupplierRequest request, ServerCallContext context) =>
        service.CreateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageCatalog)]
    public override Task<SupplierDto> UpdateSupplier(UpdateSupplierRequest request, ServerCallContext context) =>
        service.UpdateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageCatalog)]
    public override Task<OperationResult> DeleteSupplier(IdRequest request, ServerCallContext context) =>
        service.DeleteAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewCatalog)]
    public override Task<LookupList> GetSupplierLookup(Empty request, ServerCallContext context) =>
        service.GetLookupAsync(context.CancellationToken);
}
