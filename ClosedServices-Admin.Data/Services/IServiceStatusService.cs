using ClosedServices_Admin.Data.Enums;
using NodaTime;

namespace ClosedServices_Admin.Data.Services
{
    public interface IServiceStatusService
    {
        Task<ServiceCurrentStatus> GetCurrentStatus(Guid serviceId, Instant now, CancellationToken ct = default);
        Task CreateStatusUpdate(ServiceStatusUpdateCommand command, CancellationToken ct = default);
    }

    public sealed record ServiceCurrentStatus(
        ClosureState ClosureState,
        bool IsOverride,
        Instant? EffectiveFrom,
        Instant? EffectiveTo,
        string? Message);

    public sealed record ServiceStatusUpdateCommand(
        Guid ServiceId,
        ClosureState ClosureState,
        string? Message,
        Instant EffectiveFrom,
        Instant? EffectiveTo,
        string UpdatedByExternalUserId);
}
