using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class ProductGrpcService(IProductApplicationService service)
    : ProductService.ProductServiceBase
{
    [Authorize(Policy = Permissions.ViewProducts)]
    public override Task<ListProductsResponse> ListProducts(ListProductsRequest request, ServerCallContext context) =>
        service.ListAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewProducts)]
    public override Task<ProductDto> GetProduct(IdRequest request, ServerCallContext context) =>
        service.GetAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewProducts)]
    public override Task<ProductDto> GetProductBySku(GetProductBySkuRequest request, ServerCallContext context) =>
        service.GetBySkuAsync(request.Sku, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageProducts)]
    public override Task<ProductDto> CreateProduct(CreateProductRequest request, ServerCallContext context) =>
        service.CreateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageProducts)]
    public override Task<ProductDto> UpdateProduct(UpdateProductRequest request, ServerCallContext context) =>
        service.UpdateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.DeleteProducts)]
    public override Task<OperationResult> DeleteProduct(IdRequest request, ServerCallContext context) =>
        service.DeleteAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewProducts)]
    public override Task<LookupList> GetProductLookup(ProductLookupRequest request, ServerCallContext context) =>
        service.GetLookupAsync(request, context.CancellationToken);
}
