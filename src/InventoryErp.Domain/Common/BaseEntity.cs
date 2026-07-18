namespace InventoryErp.Domain.Common;

/// <summary>
/// Base for all persisted entities. Audit fields are stamped by the DbContext on save,
/// so callers never set them directly.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Soft-delete marker. Filtered out of queries by a global query filter.</summary>
    public bool IsDeleted { get; set; }
}
