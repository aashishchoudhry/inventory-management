using InventoryErp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryErp.Infrastructure.Persistence.Configurations;

public sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("QuotationLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TotalAmount).HasColumnType("decimal(18,2)");

        // Percentages, not money — two decimals is plenty.
        builder.Property(l => l.DiscountPercent).HasColumnType("decimal(5,2)");
        builder.Property(l => l.GstPercent).HasColumnType("decimal(5,2)");

        builder.Property(l => l.CreatedBy).HasMaxLength(256);
        builder.Property(l => l.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(l => l.QuotationId);
        builder.HasIndex(l => l.ProductId);

        // A line has no life of its own: deleting the parent quotation removes its lines.
        builder.HasOne<Quotation>()
            .WithMany()
            .HasForeignKey(l => l.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a product that has been quoted cannot be hard-deleted out from under the
        // quotation. Products are soft-deleted in practice, which leaves the line intact.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
