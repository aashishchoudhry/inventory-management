using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

/// <summary>
/// Global search. Two entry points over the same service: a full results page for browsing, and
/// a JSON endpoint backing the nav dropdown.
/// </summary>
[Authorize]
public class SearchController : Controller
{
    /// <summary>Kept small — a dropdown that scrolls defeats its purpose.</summary>
    private const int DropdownLimitPerType = 5;

    private const int PageLimitPerType = 25;

    private readonly ISearchService _search;
    private readonly ICurrentCompanyProvider _currentCompany;

    public SearchController(ISearchService search, ICurrentCompanyProvider currentCompany)
    {
        _search = search;
        _currentCompany = currentCompany;
    }

    /// <summary>Full results page. Shareable URL, works without JavaScript.</summary>
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var keyword = q?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
        {
            return View(new SearchViewModel());
        }

        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return View(new SearchViewModel { Keyword = keyword, Message = "No company is configured." });
        }

        var result = await _search.SearchAsync(
            companyId.Value, keyword, PageLimitPerType, cancellationToken);

        if (!result.IsSuccess)
        {
            return View(new SearchViewModel
            {
                Keyword = keyword,
                Message = result.ValidationErrors.Count > 0
                    ? string.Join(" ", result.ValidationErrors)
                    : result.Error,
            });
        }

        return View(new SearchViewModel { Keyword = keyword, Results = result.Data });
    }

    /// <summary>
    /// JSON for the nav dropdown. Returns an empty list rather than an error for a too-short
    /// keyword, since the dropdown polls as the user types and a 400 per keystroke is noise.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Suggest(string? q, CancellationToken cancellationToken)
    {
        var keyword = q?.Trim() ?? string.Empty;

        if (keyword.Length < ISearchService.MinimumKeywordLength)
        {
            return Json(new { items = Array.Empty<object>(), total = 0, truncated = false });
        }

        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return Json(new { items = Array.Empty<object>(), total = 0, truncated = false });
        }

        var result = await _search.SearchAsync(
            companyId.Value, keyword, DropdownLimitPerType, cancellationToken);

        if (!result.IsSuccess)
        {
            return Json(new { items = Array.Empty<object>(), total = 0, truncated = false });
        }

        var items = result.Data!.Items.Select(item =>
        {
            var (controller, action) = SearchResultRoutes.For(item.Type);

            return new
            {
                type = SearchResultRoutes.Label(item.Type),
                icon = SearchResultRoutes.Icon(item.Type),
                title = item.Title,
                subtitle = item.Subtitle,
                meta = item.Meta,
                url = SearchResultRoutes.HasDetailPage(item.Type)
                    ? Url.Action(action, controller, new { id = item.Id })
                    : Url.Action(action, controller),
            };
        });

        return Json(new
        {
            items,
            total = result.Data.TotalCount,
            truncated = result.Data.IsTruncated,
        });
    }
}
