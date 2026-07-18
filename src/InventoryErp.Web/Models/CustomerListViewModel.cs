using InventoryErp.Application.DTOs.Customers;
using InventoryErp.Domain.Common;

namespace InventoryErp.Web.Models;

public sealed class CustomerListViewModel
{
    public PagedResult<CustomerDto> Page { get; init; } = PagedResult<CustomerDto>.Empty(1, 20);

    /// <summary>Set when the service returned a failure, so the view can say so plainly.</summary>
    public string? LoadError { get; init; }
}
