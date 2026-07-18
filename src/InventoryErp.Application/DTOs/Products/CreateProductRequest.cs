using System.ComponentModel.DataAnnotations;
using InventoryErp.Domain.Enums;

namespace InventoryErp.Application.DTOs.Products;

public class CreateProductRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 9_999_999.99)]
    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Quantity on hand")]
    public int QuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Reorder level")]
    public int ReorderLevel { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;
}
