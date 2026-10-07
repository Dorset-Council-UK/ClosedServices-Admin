using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace ClosedServices_Admin.Data.Services
{
    public class ServiceStatusService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<ServiceStatusService> logger) : IServiceStatusService
    {
        public async Task<IReadOnlyCollection<ServiceStatusOverview>> GetStatusOverviews(IReadOnlyCollection<Guid> serviceIds, Instant now, CancellationToken ct = default)
        {
            if (serviceIds.Count == 0)
            {
                return [];
            }

            var requestedServiceIds = serviceIds
                .Where(serviceId => serviceId != Guid.Empty)
                .ToHashSet();

            if (requestedServiceIds.Count == 0)
            {
                return [];
            }

            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var services = await context.Services
                .Include(service => service.OperatingDays)
                .AsNoTracking()
                .Where(service => requestedServiceIds.Contains(service.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (services.Count == 0)
            {
                return [];
            }

            var serviceStatusUpdates = await context.ServiceStatusUpdates
                .Include(update => update.ClosureReason)
                .AsNoTracking()
                .Where(update => requestedServiceIds.Contains(update.ServiceId) && (update.EffectiveTo == null || update.EffectiveTo > now))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var statusUpdatesByServiceId = serviceStatusUpdates.ToLookup(update => update.ServiceId);

            return [.. services.Select(service =>
            {
                var updatesForService = statusUpdatesByServiceId[service.Id];
                var currentStatus = ResolveCurrentStatus(updatesForService, now);
                var upcomingStatuses = ResolveUpcomingStatusUpdates(updatesForService, now)
                    .Select(update => ToStatusUpdateSummary(update, false))
                    .ToArray();

                return new ServiceStatusOverview(service, currentStatus, upcomingStatuses);
            })];
        }

        public async Task<ServiceCurrentStatus> GetCurrentStatus(Guid serviceId, Instant now, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var update = await context.ServiceStatusUpdates
                .AsNoTracking()
                .Where(x => x.ServiceId == serviceId && (x.EffectiveTo == null || x.EffectiveTo > now))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return ResolveCurrentStatus(update, now);
        }

        public async Task CreateStatusUpdate(ServiceStatusUpdateCommand command, CancellationToken ct = default)
        {
            if (command.ServiceId == Guid.Empty)
            {
                throw new ArgumentException("A service is required.", nameof(command));
            }

            if (string.IsNullOrWhiteSpace(command.UpdatedByExternalUserId))
            {
                throw new ArgumentException("An updating user is required.", nameof(command));
            }

            if (command.EffectiveTo is not null && command.EffectiveTo <= command.EffectiveFrom)
            {
                throw new ArgumentException("The end date and time must be after the start date and time.", nameof(command));
            }

            await using var context = await contextFactory.CreateDbContextAsync(ct);
            context.ServiceStatusUpdates.Add(new ServiceStatusUpdate
            {
                Id = Guid.NewGuid(),
                ServiceId = command.ServiceId,
                ClosureState = command.ClosureState,
                ClosureReasonId = command.ClosureReason,
                Message = command.Message?.Trim(),
                EffectiveFrom = command.EffectiveFrom,
                EffectiveTo = command.EffectiveTo,
                UpdatedByExternalUserId = command.UpdatedByExternalUserId,
                UpdatedAt = SystemClock.Instance.GetCurrentInstant()
            });

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Created service status update for service {ServiceId}", command.ServiceId);
        }

        public async Task<ServiceStatusUpdateSummary?> GetCurrentOrNextStatusUpdate(Guid serviceId, Instant now, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var updates = await context.ServiceStatusUpdates
                .Include(x => x.ClosureReason)
                .AsNoTracking()
                .Where(x => x.ServiceId == serviceId && (x.EffectiveTo == null || x.EffectiveTo > now))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var currentUpdate = ResolveCurrentUpdate(updates, now);

            if (currentUpdate is not null)
            {
                return ToStatusUpdateSummary(currentUpdate, true);
            }

            var nextUpdate = ResolveUpcomingStatusUpdates(updates, now).FirstOrDefault();
            return nextUpdate is null
                ? null
                : ToStatusUpdateSummary(nextUpdate, false);
        }

        public async Task<bool> DeleteStatusUpdate(Guid serviceId, Guid statusUpdateId, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var statusUpdate = await context.ServiceStatusUpdates
                .FirstOrDefaultAsync(x => x.Id == statusUpdateId && x.ServiceId == serviceId, ct)
                .ConfigureAwait(false);

            if (statusUpdate is null)
            {
                return false;
            }

            context.ServiceStatusUpdates.Remove(statusUpdate);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Deleted service status update {StatusUpdateId} for service {ServiceId}", statusUpdateId, serviceId);
            return true;
        }

        public async Task<IReadOnlyCollection<ClosureReason>> GetClosureReasons(CancellationToken ct)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            return await context.ClosureReasons.OrderBy(c => c.Order).ToListAsync(ct);

        }

        private static bool IsCurrentOrFuture(ServiceStatusUpdate update, Instant now)
        {
            return update.EffectiveTo == null || update.EffectiveTo > now;
        }

        private static ServiceStatusUpdate? ResolveCurrentUpdate(IEnumerable<ServiceStatusUpdate> updates, Instant now)
        {
            return updates
                .Where(update => update.EffectiveFrom <= now && IsCurrentOrFuture(update, now))
                .OrderByDescending(update => update.EffectiveFrom)
                .ThenByDescending(update => update.UpdatedAt)
                .FirstOrDefault();
        }

        private static ServiceCurrentStatus ResolveCurrentStatus(IEnumerable<ServiceStatusUpdate> updates, Instant now)
        {
            var currentUpdate = ResolveCurrentUpdate(updates, now);
            return currentUpdate is null
                ? new ServiceCurrentStatus(ClosureState.NoDisruption, false, null, null, null, null)
                : new ServiceCurrentStatus(currentUpdate.ClosureState, true, currentUpdate.EffectiveFrom, currentUpdate.EffectiveTo, currentUpdate.ClosureReasonId, currentUpdate.Message);
        }

        private static IEnumerable<ServiceStatusUpdate> ResolveUpcomingStatusUpdates(IEnumerable<ServiceStatusUpdate> updates, Instant now)
        {
            return updates
                .Where(update => update.EffectiveFrom > now)
                .OrderBy(update => update.EffectiveFrom)
                .ThenByDescending(update => update.UpdatedAt);
        }

        private static ServiceStatusUpdateSummary ToStatusUpdateSummary(ServiceStatusUpdate update, bool isCurrent)
        {
            return new ServiceStatusUpdateSummary(
                update.Id,
                update.ClosureState,
                update.EffectiveFrom,
                update.EffectiveTo,
                update.ClosureReason,
                update.Message,
                isCurrent);
        }
    }
}
