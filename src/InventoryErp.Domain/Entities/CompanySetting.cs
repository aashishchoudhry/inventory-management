using InventoryErp.Domain.Common;

namespace InventoryErp.Domain.Entities;

/// <summary>
/// A single key/value setting scoped to a <see cref="Company"/>. Holds runtime-configurable
/// values (as opposed to deployment configuration in appsettings.json); the well-known keys
/// live in <c>InventoryErp.Shared.Constants.SettingKeys</c>.
/// </summary>
public class CompanySetting : BaseEntity
{
    public Guid CompanyId { get; set; }

    public required string Key { get; set; }

    public string? Value { get; set; }
}
