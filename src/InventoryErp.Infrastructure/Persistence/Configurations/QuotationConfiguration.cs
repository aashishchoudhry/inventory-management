using InventoryErp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryErp.Infrastructure.Persistence.Configurations;

public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuotationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(q => q.SubTotal).HasColumnType("decimal(18,2)");
        builder.Property(q => q.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(q => q.DiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(q => q.TotalAmount).HasColumnType("decimal(18,2)");

        builder.Property(q => q.Notes).HasMaxLength(2000);

        builder.Property(q => q.CreatedBy).HasMaxLength(256);
        builder.Property(q => q.ModifiedBy).HasMaxLength(256);

        // Quotation numbers are unique per tenant. Filtered so a soft-deleted number can be reused.
        builder.HasIndex(q => new { q.CompanyId, q.QuotationNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(q => q.CompanyId);
        builder.HasIndex(q => q.CustomerId);

        // Restrict, not cascade: deleting a customer must never silently destroy the commercial
        // record of what was quoted to them.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(q => q.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: a company owning quotations must not be deletable out from under them. The
        // application soft-deletes anyway, so no cascade would ever fire.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(q => q.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
