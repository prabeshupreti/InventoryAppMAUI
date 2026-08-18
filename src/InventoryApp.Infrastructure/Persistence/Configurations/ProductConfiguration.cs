using InventoryApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryApp.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku).IsRequired().HasMaxLength(60);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.UnitOfMeasure).HasMaxLength(20);
        builder.Property(p => p.Barcode).HasMaxLength(60);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.Location).HasMaxLength(120);

        builder.Property(p => p.UnitPrice).HasPrecision(18, 2);
        builder.Property(p => p.CostPrice).HasPrecision(18, 2);

        // CurrentStock has a private setter; tell EF to use the backing field.
        builder.Property(p => p.CurrentStock)
            .HasField("<CurrentStock>k__BackingField")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);   // categories in use cannot be removed

        builder.HasOne(p => p.Supplier)
            .WithMany(s => s.Products)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Barcode);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.SupplierId);
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.CurrentStock);
    }
}
