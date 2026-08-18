using System.Linq.Expressions;
using InventoryApp.Application.Abstractions;
using InventoryApp.Application.Common;
using InventoryApp.Application.Mapping;
using InventoryApp.Contracts.Catalog;
using InventoryApp.Contracts.Common;
using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Services;

public sealed class SupplierApplicationService(IInventoryDbContext db) : ISupplierApplicationService
{
    private static readonly Dictionary<string, Expression<Func<Supplier, object>>> SortMap = new()
    {
        ["companyName"] = s => s.CompanyName,
        ["contactPerson"] = s => s.ContactPerson,
        ["email"] = s => s.Email,
        ["createdAtUtc"] = s => s.CreatedAtUtc,
        ["isActive"] = s => s.IsActive
    };

    public async Task<ListSuppliersResponse> ListAsync(ListSuppliersRequest request, CancellationToken ct)
    {
        var search = Paging.SearchTerm(request.Page);
        var query = db.Suppliers.AsNoTracking().AsQueryable();

        if (search.Length > 0)
        {
            query = query.Where(s =>
                EF.Functions.Like(s.CompanyName, $"%{search}%") ||
                EF.Functions.Like(s.ContactPerson, $"%{search}%") ||
                EF.Functions.Like(s.Email, $"%{search}%") ||
                EF.Functions.Like(s.Phone, $"%{search}%"));
        }

        if (request.HasOnlyActive)
        {
            query = query.Where(s => s.IsActive == request.OnlyActive);
        }

        query = Paging.ApplySort(query, request.Page, SortMap, "companyName");

        var projected = query.Select(s => new { Supplier = s, ProductCount = s.Products.Count() });

        var (page, size) = Paging.Normalize(request.Page);
        var total = await projected.CountAsync(ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var rows = await projected.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        var response = new ListSuppliersResponse
        {
            PageInfo = new PageInfo { Page = page, PageSize = size, TotalCount = total, TotalPages = totalPages }
        };
        response.Items.AddRange(rows.Select(r => r.Supplier.ToDto(r.ProductCount)));
        return response;
    }

    public async Task<SupplierDto> GetAsync(int id, CancellationToken ct)
    {
        var row = await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { Supplier = s, ProductCount = s.Products.Count() })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Supplier", id);

        return row.Supplier.ToDto(row.ProductCount);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct)
    {
        var name = Guard.Required(request.CompanyName, "Company name", 200);
        Guard.Email(request.Email, "Email");

        if (await db.Suppliers.AnyAsync(s => s.CompanyName.ToLower() == name.ToLower(), ct))
        {
            throw new ConflictException($"A supplier named '{name}' already exists.");
        }

        var supplier = new Supplier
        {
            CompanyName = name,
            ContactPerson = Guard.Optional(request.ContactPerson, "Contact person", 200),
            Email = Guard.Optional(request.Email, "Email", 200),
            Phone = Guard.Optional(request.Phone, "Phone", 50),
            Address = Guard.Optional(request.Address, "Address", 500),
            Notes = Guard.Optional(request.Notes, "Notes", 1000),
            IsActive = request.IsActive
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return supplier.ToDto(0);
    }

    public async Task<SupplierDto> UpdateAsync(UpdateSupplierRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
                       ?? throw new NotFoundException("Supplier", request.Id);

        var name = Guard.Required(request.CompanyName, "Company name", 200);
        Guard.Email(request.Email, "Email");

        if (await db.Suppliers.AnyAsync(s => s.Id != request.Id && s.CompanyName.ToLower() == name.ToLower(), ct))
        {
            throw new ConflictException($"A supplier named '{name}' already exists.");
        }

        supplier.CompanyName = name;
        supplier.ContactPerson = Guard.Optional(request.ContactPerson, "Contact person", 200);
        supplier.Email = Guard.Optional(request.Email, "Email", 200);
        supplier.Phone = Guard.Optional(request.Phone, "Phone", 50);
        supplier.Address = Guard.Optional(request.Address, "Address", 500);
        supplier.Notes = Guard.Optional(request.Notes, "Notes", 1000);
        supplier.IsActive = request.IsActive;
        supplier.Touch();

        await db.SaveChangesAsync(ct);

        var productCount = await db.Products.CountAsync(p => p.SupplierId == supplier.Id, ct);
        return supplier.ToDto(productCount);
    }

    public async Task<OperationResult> DeleteAsync(int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
                       ?? throw new NotFoundException("Supplier", id);

        var productCount = await db.Products.CountAsync(p => p.SupplierId == id, ct);
        if (productCount > 0)
        {
            throw new ConflictException(
                $"'{supplier.CompanyName}' supplies {productCount} product(s) and cannot be deleted. " +
                "Reassign those products first, or deactivate the supplier instead.");
        }

        var orderCount = await db.PurchaseOrders.CountAsync(p => p.SupplierId == id, ct);
        if (orderCount > 0)
        {
            throw new ConflictException(
                $"'{supplier.CompanyName}' has {orderCount} purchase order(s) in history and cannot be deleted. " +
                "Deactivate the supplier instead to keep the audit trail intact.");
        }

        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
        return new OperationResult { Success = true, Message = $"Supplier '{supplier.CompanyName}' deleted." };
    }

    public async Task<LookupList> GetLookupAsync(CancellationToken ct)
    {
        var items = await db.Suppliers.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.CompanyName)
            .Select(s => new LookupItem { Id = s.Id, Name = s.CompanyName })
            .ToListAsync(ct);

        var list = new LookupList();
        list.Items.AddRange(items);
        return list;
    }
}
