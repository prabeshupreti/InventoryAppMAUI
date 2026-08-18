using System.Linq.Expressions;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Services;

public sealed class CategoryApplicationService(IInventoryDbContext db) : ICategoryApplicationService
{
    private static readonly Dictionary<string, Expression<Func<Category, object>>> SortMap = new()
    {
        ["name"] = c => c.Name,
        ["createdAtUtc"] = c => c.CreatedAtUtc,
        ["updatedAtUtc"] = c => c.UpdatedAtUtc,
        ["isActive"] = c => c.IsActive
    };

    public async Task<ListCategoriesResponse> ListAsync(ListCategoriesRequest request, CancellationToken ct)
    {
        var search = Paging.SearchTerm(request.Page);
        var query = db.Categories.AsNoTracking().AsQueryable();

        if (search.Length > 0)
        {
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{search}%") ||
                EF.Functions.Like(c.Description, $"%{search}%"));
        }

        if (request.HasOnlyActive)
        {
            query = query.Where(c => c.IsActive == request.OnlyActive);
        }

        query = Paging.ApplySort(query, request.Page, SortMap, "name");

        // Project the product count in the same round trip instead of an N+1 per row.
        var projected = query.Select(c => new
        {
            Category = c,
            ProductCount = c.Products.Count()
        });

        var (page, size) = Paging.Normalize(request.Page);
        var total = await projected.CountAsync(ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var rows = await projected.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        var response = new ListCategoriesResponse
        {
            PageInfo = new PageInfo { Page = page, PageSize = size, TotalCount = total, TotalPages = totalPages }
        };
        response.Items.AddRange(rows.Select(r => r.Category.ToDto(r.ProductCount)));
        return response;
    }

    public async Task<CategoryDto> GetAsync(int id, CancellationToken ct)
    {
        var row = await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { Category = c, ProductCount = c.Products.Count() })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Category", id);

        return row.Category.ToDto(row.ProductCount);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var name = Guard.Required(request.Name, "Category name", 120);

        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }

        var category = new Category
        {
            Name = name,
            Description = Guard.Optional(request.Description, "Description", 500),
            IsActive = request.IsActive
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.ToDto(0);
    }

    public async Task<CategoryDto> UpdateAsync(UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
                       ?? throw new NotFoundException("Category", request.Id);

        var name = Guard.Required(request.Name, "Category name", 120);

        if (await db.Categories.AnyAsync(c => c.Id != request.Id && c.Name.ToLower() == name.ToLower(), ct))
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }

        var productCount = await db.Products.CountAsync(p => p.CategoryId == category.Id, ct);

        if (!request.IsActive && category.IsActive && productCount > 0)
        {
            throw new ConflictException(
                $"'{category.Name}' still has {productCount} product(s). Move them to another category before deactivating it.");
        }

        category.Name = name;
        category.Description = Guard.Optional(request.Description, "Description", 500);
        category.IsActive = request.IsActive;
        category.Touch();

        await db.SaveChangesAsync(ct);
        return category.ToDto(productCount);
    }

    public async Task<OperationResult> DeleteAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Category", id);

        var productCount = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        if (productCount > 0)
        {
            throw new ConflictException(
                $"'{category.Name}' is used by {productCount} product(s) and cannot be deleted. " +
                "Reassign those products first, or deactivate the category instead.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return new OperationResult { Success = true, Message = $"Category '{category.Name}' deleted." };
    }

    public async Task<LookupList> GetLookupAsync(CancellationToken ct)
    {
        var items = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItem { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);

        var list = new LookupList();
        list.Items.AddRange(items);
        return list;
    }
}
