namespace InventoryApp.Application.Abstractions;

/// <summary>Produces human-friendly document numbers such as PO-2025-0007 / SO-2025-0031.</summary>
public interface IDocumentNumberGenerator
{
    Task<string> NextPurchaseNumberAsync(CancellationToken cancellationToken);
    Task<string> NextSaleNumberAsync(CancellationToken cancellationToken);
}
