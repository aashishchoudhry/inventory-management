using InventoryErp.Application.Interfaces;

namespace InventoryErp.Web.Services;

/// <summary>Reads the caller's identity from the current HTTP context for audit stamping.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserName => _httpContextAccessor.HttpContext?.User.Identity?.Name;
}
