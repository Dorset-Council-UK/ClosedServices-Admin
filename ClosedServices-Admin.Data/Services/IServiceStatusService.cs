using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using NodaTime;

namespace ClosedServices_Admin.Data.Services
{
    public interface IServiceStatusService
    {
        Task<ServiceCurrentStatus> GetCurrentStatus(Guid serviceId, Instant now, CancellationToken ct = default);
        Task<IReadOnlyCollection<ServiceStatusOverview>> GetStatusOverviews(IReadOnlyCollection<Guid> serviceIds, Instant now, CancellationToken ct = default);
        Task<ServiceStatusUpdateSummary?> GetCurrentOrNextStatusUpdate(Guid serviceId, Instant now, CancellationToken ct = default);
        Task<IReadOnlyCollection<ServiceStatusUpdateSummary>> GetCurrentAndUpcomingStatusUpdates(Guid serviceId, Instant now, CancellationToken ct = default);
        Task CreateStatusUpdate(ServiceStatusUpdateCommand command, CancellationToken ct = default);
        Task<bool> DeleteStatusUpdate(Guid serviceId, Guid statusUpdateId, CancellationToken ct = default);
        Task<IReadOnlyCollection<ClosureReason>> GetClosureReasons(CancellationToken ct = default);
    }

    public sealed record ServiceCurrentStatus(
        ClosureState ClosureState,
        bool IsOverride,
        Instant? EffectiveFrom,
        Instant? EffectiveTo,
        Guid? ClosureReason,
        string? Message);

    public sealed record ServiceStatusUpdateCommand(
        Guid ServiceId,
        ClosureState ClosureState,
        Guid? ClosureReason,
        string? Message,
        Instant EffectiveFrom,
        Instant? EffectiveTo,
        string UpdatedByExternalUserId);

    public sealed record ServiceStatusUpdateSummary(
        Guid Id,
        ClosureState ClosureState,
        Instant EffectiveFrom,
        Instant? EffectiveTo,
        ClosureReason? ClosureReason,
        string? Message,
        bool IsCurrent);

    public sealed record ServiceStatusOverview(
        Service Service,
        ServiceCurrentStatus CurrentStatus,
        IReadOnlyCollection<ServiceStatusUpdateSummary> UpcomingStatuses);
}
