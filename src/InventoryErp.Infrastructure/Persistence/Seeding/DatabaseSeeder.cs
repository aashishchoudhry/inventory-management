using InventoryErp.Infrastructure.Identity;
using InventoryErp.Shared.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryErp.Infrastructure.Persistence.Seeding;

/// <summary>
/// Writes sample data on first run. Safe to call on every startup: it does nothing once a company
/// exists, so an existing database is never modified.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly InventoryErpDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        InventoryErpDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Roles are checked independently of the emptiness guard: they must exist even on a
        // database that was seeded before a role was added.
        await SeedRolesAsync();

        // A company is the tenant root — if one exists, the database has been seeded.
        if (await _context.Companies.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Seed skipped: database already contains a company.");
            return;
        }

        _logger.LogInformation("Seeding database with sample data.");

        var company = SeedData.CreateCompany();
        await _context.Companies.AddAsync(company, cancellationToken);

        await _context.Products.AddRangeAsync(SeedData.CreateProducts(company.Id), cancellationToken);
        await _context.Customers.AddRangeAsync(SeedData.CreateCustomers(company.Id), cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await SeedAdminUserAsync();

        _logger.LogInformation(
            "Seed complete: 1 company, {ProductCount} products, {CustomerCount} customers.",
            await _context.Products.CountAsync(cancellationToken),
            await _context.Customers.CountAsync(cancellationToken));
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (await _roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new ApplicationRole(role));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{role}': {Describe(result)}");
            }

            _logger.LogInformation("Created role {Role}.", role);
        }
    }

    private async Task SeedAdminUserAsync()
    {
        if (await _userManager.FindByEmailAsync(SeedData.AdminEmail) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = SeedData.AdminEmail,
            Email = SeedData.AdminEmail,
            FullName = SeedData.AdminFullName,
            EmailConfirmed = true,
            IsActive = true,
        };

        var created = await _userManager.CreateAsync(user, SeedData.AdminPassword);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create the seed admin user: {Describe(created)}");
        }

        var assigned = await _userManager.AddToRoleAsync(user, Roles.Admin);

        if (!assigned.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign the {Roles.Admin} role: {Describe(assigned)}");
        }

        _logger.LogInformation("Created seed admin user {Email}.", SeedData.AdminEmail);
    }

    private static string Describe(IdentityResult result)
        => string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
