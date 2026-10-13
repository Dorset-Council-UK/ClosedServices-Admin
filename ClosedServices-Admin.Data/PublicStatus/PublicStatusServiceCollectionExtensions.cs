using ClosedServices_Admin.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ClosedServices_Admin.Data.PublicStatus;

public static class PublicStatusServiceCollectionExtensions
{
    public static IServiceCollection AddPublicStatusQueries(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.TryAddScoped<IServiceStatusService, ServiceStatusService>();
        services.AddScoped<IPublicStatusService, PublicStatusService>();
        return services;
    }
}
