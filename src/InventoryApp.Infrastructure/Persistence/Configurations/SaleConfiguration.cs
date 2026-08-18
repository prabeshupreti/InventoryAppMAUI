using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SaleNumber).IsRequired().HasMaxLength(40);
        builder.Property(s => s.CustomerName).HasMaxLength(200);
        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.Property(s => s.CreatedByName).HasMaxLength(200);
        builder.Property(s => s.Status).HasConversion<int>();

        builder.Property(s => s.Subtotal).HasPrecision(18, 2);
        builder.Property(s => s.TaxRate).HasPrecision(9, 2);
        builder.Property(s => s.TaxAmount).HasPrecision(18, 2);
        builder.Property(s => s.DiscountAmount).HasPrecision(18, 2);
        builder.Property(s => s.Total).HasPrecision(18, 2);

        builder.HasMany(s => s.Items)
            .WithOne(i => i.Sale!)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.SaleNumber).IsUnique();
        builder.HasIndex(s => s.SaleDateUtc);
        builder.HasIndex(s => s.Status);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.LineTotal).HasPrecision(18, 2);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.SaleId);
        builder.HasIndex(i => i.ProductId);
    }
}
