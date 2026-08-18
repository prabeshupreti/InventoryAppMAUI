namespace InventoryApp.Domain.Entities;

/// <summary>Base class carrying creation/modification timestamps (UTC).</summary>
public abstract class AuditableEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
