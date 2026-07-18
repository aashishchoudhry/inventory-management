using System.ComponentModel.DataAnnotations;

namespace InventoryErp.Web.Models;

/// <summary>
/// Form model for company settings. Mirrors <c>CompanySettingsDto</c> but carries the display
/// names and client-side validation attributes the view needs.
/// </summary>
public sealed class SettingsViewModel
{
    [Required(ErrorMessage = "Company name is required.")]
    [StringLength(200)]
    [Display(Name = "Company name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string Tagline { get; set; } = string.Empty;

    [StringLength(20)]
    public string Mobile { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(256)]
    public string Website { get; set; } = string.Empty;

    /// <summary>Existing stored path, round-tripped so a save without a new upload keeps it.</summary>
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>Set only when the user picks a new file.</summary>
    [Display(Name = "Logo")]
    public IFormFile? LogoFile { get; set; }

    /// <summary>Ticked to clear the current logo.</summary>
    [Display(Name = "Remove current logo")]
    public bool RemoveLogo { get; set; }

    [StringLength(500)]
    [Display(Name = "Address")]
    public string AddressLine { get; set; } = string.Empty;

    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [StringLength(100)]
    public string State { get; set; } = string.Empty;

    [StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "PIN code")]
    public string PinCode { get; set; } = string.Empty;

    // Blank or exactly 15 — a company may not be GST-registered. MinimumLength would reject
    // blank and contradict the service, which permits it.
    [RegularExpression("^$|^.{15}$", ErrorMessage = "GSTIN must be exactly 15 characters.")]
    [Display(Name = "GSTIN")]
    public string Gstin { get; set; } = string.Empty;

    [RegularExpression("^$|^.{10}$", ErrorMessage = "PAN must be exactly 10 characters.")]
    [Display(Name = "PAN")]
    public string Pan { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Invoice terms")]
    public string InvoiceTerms { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Invoice footer")]
    public string InvoiceFooter { get; set; } = string.Empty;

    [RegularExpression("^$|^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        ErrorMessage = "Use a hex colour such as #4F46E5.")]
    [Display(Name = "Primary accent colour")]
    public string PrimaryAccentColor { get; set; } = "#4F46E5";
}
