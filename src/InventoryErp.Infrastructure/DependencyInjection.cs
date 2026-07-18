using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Interfaces;
using InventoryErp.Infrastructure.Documents;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using InventoryErp.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryErp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<InventoryErpDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Identity itself is registered by the web composition root, because the default
        // Identity UI is an ASP.NET-only concern that this layer must not depend on.

        // Persistence implementations only. Application services (IProductService and friends)
        // live in the Application layer and are registered by AddApplication.
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IQuotationPdfService, QuotationPdfService>();

        // QuestPDF requires an explicit licence declaration at startup. Community is free for
        // organisations under the revenue threshold — see design-notes.md.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        return services;
    }
}
