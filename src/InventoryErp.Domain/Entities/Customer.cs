using InventoryErp.Domain.Common;

namespace InventoryErp.Domain.Entities;

/// <summary>
/// A party the company sells to. Scoped to a <see cref="Company"/> for tenant isolation.
/// </summary>
public class Customer : BaseEntity
{
    /// <summary>Owning tenant. Every query for customers must filter on this.</summary>
    public Guid CompanyId { get; set; }

    public required string Name { get; set; }

    /// <summary>Human-readable identifier, unique within the company.</summary>
    public string? Code { get; set; }

    public string? Mobile { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Address { get; set; }
}
