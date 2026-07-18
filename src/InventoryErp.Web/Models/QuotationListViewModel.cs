using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Domain.Common;

namespace InventoryErp.Web.Models;

public sealed class QuotationListViewModel
{
    public PagedResult<QuotationListItemDto> Page { get; init; }
        = PagedResult<QuotationListItemDto>.Empty(1, 20);

    /// <summary>Set when the service returned a failure, so the view can say so plainly.</summary>
    public string? LoadError { get; init; }
}
