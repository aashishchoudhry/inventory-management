using InventoryErp.Domain.Common;

namespace InventoryErp.Domain.Entities;

/// <summary>
/// The organisation the system is being run for. Its details are the source of truth for
/// document branding (invoices, PDFs) and statutory identifiers.
/// </summary>
public class Company : BaseEntity
{
    public required string Name { get; set; }

    public string? Tagline { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public string? PinCode { get; set; }

    /// <summary>Goods and Services Tax identification number.</summary>
    public string? GstNumber { get; set; }

    /// <summary>Permanent Account Number.</summary>
    public string? PanNumber { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    /// <summary>Path to the logo used on generated documents.</summary>
    public string? LogoPath { get; set; }
}
