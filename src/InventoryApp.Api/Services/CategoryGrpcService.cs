using Grpc.Core;
using InventoryApp.Application.Abstractions;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Contracts.Security;
using Microsoft.AspNetCore.Authorization;

namespace InventoryApp.Api.Services;

[Authorize]
public sealed class CategoryGrpcService(ICategoryApplicationService service)
    : CategoryService.CategoryServiceBase
{
    [Authorize(Policy = Permissions.ViewCatalog)]
    public override Task<ListCategoriesResponse> ListCategories(ListCategoriesRequest request, ServerCallContext context) =>
        service.ListAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewCatalog)]
    public override Task<CategoryDto> GetCategory(IdRequest request, ServerCallContext context) =>
        service.GetAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageCatalog)]
    public override Task<CategoryDto> CreateCategory(CreateCategoryRequest request, ServerCallContext context) =>
        service.CreateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageCatalog)]
    public override Task<CategoryDto> UpdateCategory(UpdateCategoryRequest request, ServerCallContext context) =>
        service.UpdateAsync(request, context.CancellationToken);

    [Authorize(Policy = Permissions.ManageCatalog)]
    public override Task<OperationResult> DeleteCategory(IdRequest request, ServerCallContext context) =>
        service.DeleteAsync(request.Id, context.CancellationToken);

    [Authorize(Policy = Permissions.ViewCatalog)]
    public override Task<LookupList> GetCategoryLookup(Empty request, ServerCallContext context) =>
        service.GetLookupAsync(context.CancellationToken);
}
