using System.ComponentModel.DataAnnotations;
using InventoryErp.Domain.Enums;

namespace InventoryErp.Application.DTOs.Products;

public class CreateProductRequest
{
    [Required]
    [Display(Name = "Company")]
    public Guid CompanyId { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 1)]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Barcode { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 9_999_999.99)]
    [Display(Name = "Selling price")]
    public decimal SellingPrice { get; set; }

    [Range(0, 100)]
    [Display(Name = "GST %")]
    public decimal GstPercent { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Current stock")]
    public int CurrentStock { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Reorder level")]
    public int ReorderLevel { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;
}
