using InventoryErp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryErp.Infrastructure.Persistence.Configurations;

public sealed class CompanySettingConfiguration : IEntityTypeConfiguration<CompanySetting>
{
    public void Configure(EntityTypeBuilder<CompanySetting> builder)
    {
        builder.ToTable("CompanySettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200);

        // Values are untyped text; no length cap, since settings may hold anything
        // from a flag to a block of footer text.
        builder.Property(s => s.Value);

        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.ModifiedBy).HasMaxLength(256);

        // One row per key per company. Filtered so a soft-deleted key can be re-added.
        builder.HasIndex(s => new { s.CompanyId, s.Key })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
