using InventoryErp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryErp.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Barcode)
            .HasMaxLength(50);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.SellingPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.GstPercent)
            .HasColumnType("decimal(5,2)");

        builder.Property(p => p.Status)
            .HasConversion<int>();

        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.ModifiedBy).HasMaxLength(256);

        // SKU is unique per tenant, not globally. Filtered so a soft-deleted SKU can be reused.
        builder.HasIndex(p => new { p.CompanyId, p.Sku })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(p => p.CompanyId);

        builder.Ignore(p => p.IsBelowReorderLevel);
    }
}
