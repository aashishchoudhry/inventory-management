using Microsoft.AspNetCore.Identity;

namespace InventoryErp.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    public bool IsActive { get; set; } = true;
}
