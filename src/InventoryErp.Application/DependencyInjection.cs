using Microsoft.Extensions.DependencyInjection;

namespace InventoryErp.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application-layer services. Service *implementations* currently live in
    /// Infrastructure (they need EF Core), so this is a placeholder for validators,
    /// mappers and pure application services as they arrive.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
