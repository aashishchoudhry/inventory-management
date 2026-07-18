using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

/// <summary>
/// Read-only customer listing. Create, edit and delete are out of scope (Stretch).
/// </summary>
[Authorize]
public class CustomersController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly ICustomerService _customerService;
    private readonly ICurrentCompanyProvider _currentCompany;

    public CustomersController(ICustomerService customerService, ICurrentCompanyProvider currentCompany)
    {
        _customerService = customerService;
        _currentCompany = currentCompany;
    }

    public async Task<IActionResult> Index(
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return View(new CustomerListViewModel
            {
                LoadError = "No company has been configured yet, so there are no customers to show.",
            });
        }

        var result = await _customerService.GetAllAsync(
            companyId.Value,
            page,
            pageSize ?? DefaultPageSize,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return View(new CustomerListViewModel
            {
                // Out-of-range paging is reported rather than silently clamped, so the user
                // sees why they are not looking at what they asked for.
                LoadError = result.ValidationErrors.Count > 0
                    ? string.Join(" ", result.ValidationErrors)
                    : result.Error,
            });
        }

        return View(new CustomerListViewModel { Page = result.Data! });
    }
}
