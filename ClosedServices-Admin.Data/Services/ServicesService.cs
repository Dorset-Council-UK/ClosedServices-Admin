using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClosedServices_Admin.Data.Services
{
    public class ServicesService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<ServicesService> logger) : IServicesService
    {
        public async Task<IReadOnlyCollection<Service>> GetAllServices(CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.Services
                .AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyCollection<ServiceType>> GetServiceTypes(CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.ServiceTypes
                .AsNoTracking()
                .OrderBy(type => type.Order)
                .ThenBy(type => type.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<ServiceType?> GetServiceTypeByName(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var normalised = name.Trim().ToLower();
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.ServiceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name.ToLower() == normalised, ct)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyCollection<Service>> GetServicesByType(ServiceType serviceType, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.Services
                .Where(s => s.ServiceTypeId == serviceType.Id)
                .AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<Service?> GetService(Guid serviceId, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.Services
                .Include(s => s.OperatingDays)
                .Where(s => s.Id == serviceId)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<Service?> GetServiceForUser(string userId, Guid serviceId, CancellationToken ct = default)
        {
            var services = await GetServicesForUser(userId, ct).ConfigureAwait(false);
            return services.FirstOrDefault(service => service.Id == serviceId);
        }

        public async Task<IReadOnlyCollection<Service>> GetServicesForUser(string userId, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var permissions = await context.UserServicePermissions
                .AsNoTracking()
                .Where(permission => permission.ExternalUserId == userId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (permissions.Count == 0)
            {
                return [];
            }

            var serviceIds = permissions
                .Where(permission => permission.ServiceId.HasValue)
                .Select(permission => permission.ServiceId!.Value)
                .ToHashSet();

            var serviceTypes = permissions
                .Where(permission => permission.ServiceTypeId.HasValue)
                .Select(permission => permission.ServiceTypeId!.Value)
                .ToHashSet();

            return await context.Services
                .Include(service => service.OperatingDays)
                .Include(s => s.ServiceType)
                .AsNoTracking()
                .Where(service => serviceIds.Contains(service.Id) || serviceTypes.Contains(service.ServiceTypeId))
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyCollection<Service>> GetServicesForUserByType(string userId, ServiceType serviceType, CancellationToken ct = default)
        {
            var allServices = await GetServicesForUser(userId, ct);
            return [.. allServices.Where(service => service.ServiceTypeId == serviceType.Id)];
        }

    }
}
