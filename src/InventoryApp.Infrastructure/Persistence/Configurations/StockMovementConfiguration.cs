using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MovementType).HasConversion<int>();
        builder.Property(m => m.Reason).HasMaxLength(250);
        builder.Property(m => m.Reference).HasMaxLength(100);
        builder.Property(m => m.UserName).HasMaxLength(200);
        builder.Property(m => m.FromLocation).HasMaxLength(120);
        builder.Property(m => m.ToLocation).HasMaxLength(120);

        builder.HasOne(m => m.Product)
            .WithMany(p => p.StockMovements)
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // The movement log is queried by product and by date far more than anything else.
        builder.HasIndex(m => m.ProductId);
        builder.HasIndex(m => m.CreatedAtUtc);
        builder.HasIndex(m => m.MovementType);
        builder.HasIndex(m => new { m.ProductId, m.CreatedAtUtc });
    }
}
