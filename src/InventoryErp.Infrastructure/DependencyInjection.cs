using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Interfaces;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using InventoryErp.Infrastructure.Services;
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

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Identity itself is registered by the web composition root, because the default
        // Identity UI is an ASP.NET-only concern that this layer must not depend on.

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProductService, ProductService>();

        return services;
    }
}
