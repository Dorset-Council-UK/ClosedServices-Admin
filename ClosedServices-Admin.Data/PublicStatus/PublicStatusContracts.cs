namespace ClosedServices_Admin.Data.PublicStatus;

public sealed record PublicStatusFilter(string? ServiceType, Guid? ServiceId);

public sealed record PublicClosure(
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    string? Message);

public sealed record PublicServiceStatus(
    Guid ServiceId,
    string Name,
    string ServiceType,
    string Status,
    PublicClosure? CurrentClosure,
    IReadOnlyList<PublicClosure> UpcomingClosures);

public sealed record PublicStatusSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PublicServiceStatus> Services);

public sealed record PublicServiceStatusResponse(DateTimeOffset GeneratedAt, PublicServiceStatus Service);

public sealed record PublicStatusResult(PublicStatusSnapshot? Snapshot, string? NotFoundReason);

public interface IPublicStatusService
{
    Task<PublicStatusResult> GetStatusAsync(PublicStatusFilter filter, CancellationToken cancellationToken = default);
}
