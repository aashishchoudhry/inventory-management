using InventoryErp.Application.Interfaces;
using InventoryErp.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryErp.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application-layer services. These depend only on the abstractions in
    /// <c>InventoryErp.Domain.Interfaces</c>; their persistence implementations are supplied by
    /// <c>AddInfrastructure</c>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ICurrentCompanyProvider, CurrentCompanyProvider>();

        return services;
    }
}
