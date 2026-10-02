using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;

namespace ClosedServices_Admin.Data.Services
{
    public interface IServicesService
    {
        Task<IReadOnlyCollection<Service>> GetAllServices(CancellationToken ct = default);
        Task<Service?> GetService(Guid serviceId, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetServicesByType(ServiceType serviceType, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetServicesForUser(string userId, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetServicesForUserByType(string userId, ServiceType serviceType, CancellationToken ct = default);
    }
}