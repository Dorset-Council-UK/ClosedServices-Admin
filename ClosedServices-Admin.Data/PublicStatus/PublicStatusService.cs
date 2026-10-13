using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Services;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ClosedServices_Admin.Data.PublicStatus;

public sealed class PublicStatusService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IServiceStatusService statusService,
    IClock clock) : IPublicStatusService
{
    public async Task<PublicStatusResult> GetStatusAsync(PublicStatusFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (string.IsNullOrWhiteSpace(filter.ServiceType) && !filter.ServiceId.HasValue)
        {
            throw new ArgumentException("At least one service filter is required.", nameof(filter));
        }

        var now = clock.GetCurrentInstant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        Guid? serviceTypeId = null;
        if (filter.ServiceType is not null)
        {
            var normalisedName = filter.ServiceType.Trim().ToLowerInvariant();
            serviceTypeId = await context.ServiceTypes.AsNoTracking()
                .Where(type => type.Name.ToLower() == normalisedName)
                .Select(type => (Guid?)type.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!serviceTypeId.HasValue)
            {
                return new PublicStatusResult(null, "Service type not found.");
            }
        }

        var query = context.Services.AsNoTracking().Where(service => service.IsActive);
        if (serviceTypeId.HasValue)
        {
            query = query.Where(service => service.ServiceTypeId == serviceTypeId.Value);
        }
        if (filter.ServiceId.HasValue)
        {
            query = query.Where(service => service.Id == filter.ServiceId.Value);
        }
        var services = await query
            .Select(service => new { service.Id, service.Name, ServiceType = service.ServiceType!.Name })
            .ToListAsync(cancellationToken);

        if (filter.ServiceId.HasValue && services.Count == 0)
        {
            return new PublicStatusResult(null, "Active service not found for the supplied filters.");
        }

        var overviews = await statusService.GetStatusOverviews(
            services.Select(service => service.Id).ToArray(), now, cancellationToken);
        var reasonIds = overviews.Where(overview => overview.CurrentStatus.ClosureReason.HasValue)
            .Select(overview => overview.CurrentStatus.ClosureReason!.Value).Distinct().ToArray();
        var reasons = reasonIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await context.ClosureReasons.AsNoTracking().Where(reason => reasonIds.Contains(reason.Id))
                .ToDictionaryAsync(reason => reason.Id, reason => reason.Name, cancellationToken);
        var overviewById = overviews.ToDictionary(overview => overview.Service.Id);

        var results = services.OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(service => service.Id)
            .Where(service => overviewById.ContainsKey(service.Id))
            .Select(service =>
            {
                var overview = overviewById[service.Id];
                var current = overview.CurrentStatus;
                var currentClosure = current.IsOverride && current.EffectiveFrom.HasValue
                    ? new PublicClosure(ToPublicStatus(current.ClosureState), current.EffectiveFrom.Value.ToDateTimeOffset(),
                        current.EffectiveTo?.ToDateTimeOffset(),
                        current.ClosureReason.HasValue ? reasons.GetValueOrDefault(current.ClosureReason.Value) : null,
                        current.Message)
                    : null;
                var upcoming = overview.UpcomingStatuses.Select(closure => new PublicClosure(
                    ToPublicStatus(closure.ClosureState), closure.EffectiveFrom.ToDateTimeOffset(),
                    closure.EffectiveTo?.ToDateTimeOffset(), closure.ClosureReason?.Name, closure.Message)).ToArray();
                return new PublicServiceStatus(service.Id, service.Name, service.ServiceType,
                    ToPublicStatus(current.ClosureState), currentClosure, upcoming);
            }).ToArray();

        if (filter.ServiceId.HasValue && results.Length == 0)
        {
            return new PublicStatusResult(null, "Active service not found for the supplied filters.");
        }
        return new PublicStatusResult(new PublicStatusSnapshot(now.ToDateTimeOffset(), results), null);
    }

    private static string ToPublicStatus(ClosureState state) => state switch
    {
        ClosureState.NoDisruption => "noDisruption",
        ClosureState.Closed => "closed",
        ClosureState.PartiallyClosed => "partiallyClosed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown closure state.")
    };
}
