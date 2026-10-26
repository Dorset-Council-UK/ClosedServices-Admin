using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;

namespace ClosedServices_Admin.Data.Services
{
    public interface IServicesService
    {
        Task<ServiceType?> GetServiceTypeByName(string name, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetAllServices(CancellationToken ct = default);
        Task<IReadOnlyCollection<ServiceType>> GetServiceTypes(CancellationToken ct = default);
        Task<Service?> GetService(Guid serviceId, CancellationToken ct = default);
        Task<Service?> GetServiceForUser(string userId, Guid serviceId, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetServicesByType(ServiceType serviceType, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetServicesForUser(string userId, CancellationToken ct = default);
        Task<IReadOnlyCollection<Service>> GetServicesForUserByType(string userId, ServiceType serviceType, CancellationToken ct = default);
    }
}