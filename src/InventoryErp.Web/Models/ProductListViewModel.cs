using InventoryErp.Application.DTOs.Products;
using InventoryErp.Domain.Common;

namespace InventoryErp.Web.Models;

public sealed class ProductListViewModel
{
    public PagedResult<ProductDto> Page { get; init; } = PagedResult<ProductDto>.Empty(1, 20);

    /// <summary>The search term echoed back so the box keeps its value after submit.</summary>
    public string? Keyword { get; init; }

    public bool IsSearch => !string.IsNullOrWhiteSpace(Keyword);

    /// <summary>Set when the service returned a failure, so the view can say so plainly.</summary>
    public string? LoadError { get; init; }
}
