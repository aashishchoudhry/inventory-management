using System.ComponentModel.DataAnnotations;

namespace InventoryErp.Application.DTOs.Products;

public sealed class UpdateProductRequest : CreateProductRequest
{
    [Required]
    public Guid Id { get; set; }
}
