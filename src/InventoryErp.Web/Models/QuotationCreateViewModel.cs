using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryErp.Web.Models;

/// <summary>
/// Form model for creating a quotation. Separate from <c>CreateQuotationRequest</c> because MVC
/// model binding needs a mutable <see cref="List{T}"/> for the repeating lines, and the view needs
/// the dropdown option lists.
/// </summary>
public sealed class QuotationCreateViewModel
{
    // Nullable so an empty <option> binds to null and [Required] can produce a readable
    // message. A non-nullable Guid fails model binding first, yielding the framework's
    // "The value '' is invalid."
    [Required(ErrorMessage = "Select a customer.")]
    [Display(Name = "Customer")]
    public Guid? CustomerId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Quotation date")]
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    [Display(Name = "Valid until")]
    public DateTime? ValidUntil { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public List<QuotationLineInputModel> Lines { get; set; } = [];

    /// <summary>Repopulated on every render; never round-tripped through the form.</summary>
    public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> ProductOptions { get; set; } = [];

    /// <summary>Product id → default price/GST, used to prefill a line when a product is picked.</summary>
    public IReadOnlyDictionary<Guid, ProductDefaults> ProductDefaults { get; set; }
        = new Dictionary<Guid, ProductDefaults>();
}

public sealed class QuotationLineInputModel
{
    /// <summary>Nullable for the same binding reason as <c>CustomerId</c>. The service reports
    /// the missing product per line, so no attribute is needed here.</summary>
    [Display(Name = "Product")]
    public Guid? ProductId { get; set; }

    [Display(Name = "Qty")]
    public int Quantity { get; set; } = 1;

    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Disc %")]
    public decimal DiscountPercent { get; set; }

    [Display(Name = "GST %")]
    public decimal GstPercent { get; set; }
}

public sealed record ProductDefaults(decimal SellingPrice, decimal GstPercent);
