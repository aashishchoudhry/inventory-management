namespace InventoryErp.Application.DTOs.Settings;

/// <summary>
/// A strongly-typed view over the <c>CompanySetting</c> key/value rows. Every property is
/// non-null: a missing key resolves to the company's typed column, then to a hard-coded default,
/// so callers never deal with nulls or absent keys.
/// </summary>
public sealed record CompanySettingsDto
{
    // ------------------------------------------------------------- identity

    public string Name { get; init; } = string.Empty;

    public string Tagline { get; init; } = string.Empty;

    // -------------------------------------------------------------- address

    public string AddressLine { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string PinCode { get; init; } = string.Empty;

    // -------------------------------------------------------------- contact

    public string Mobile { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;

    /// <summary>
    /// Root-relative web path to the logo, e.g. <c>/uploads/logos/{guid}.png</c>. A path, never
    /// binary data — see <c>data-model.md</c>.
    /// </summary>
    public string LogoPath { get; init; } = string.Empty;

    // ------------------------------------------------------------------ tax

    /// <summary>Goods and Services Tax identification number (15 characters).</summary>
    public string Gstin { get; init; } = string.Empty;

    /// <summary>Permanent Account Number (10 characters).</summary>
    public string Pan { get; init; } = string.Empty;

    // ------------------------------------------------------------ documents

    /// <summary>Terms and conditions printed on quotations and invoices.</summary>
    public string InvoiceTerms { get; init; } = string.Empty;

    /// <summary>Footer line printed at the bottom of generated documents.</summary>
    public string InvoiceFooter { get; init; } = string.Empty;

    /// <summary>Brand colour for generated documents, as a <c>#RRGGBB</c> hex string.</summary>
    public string PrimaryAccentColor { get; init; } = DefaultAccentColor;

    public const string DefaultAccentColor = "#4F46E5";
}
