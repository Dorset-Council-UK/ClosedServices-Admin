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
        public async Task<ServiceCurrentStatus> GetCurrentStatus(Guid serviceId, Instant now, CancellationToken ct = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var update = await context.ServiceStatusUpdates
                .AsNoTracking()
                .Where(x => x.ServiceId == serviceId && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                .OrderByDescending(x => x.EffectiveFrom)
                .ThenByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (update is not null)
            {
                return new(update.ClosureState, true, update.EffectiveFrom, update.EffectiveTo, update.Message);
            }

            return new(ClosureState.NoDisruption, false, null, null, null);
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
                Message = command.Message?.Trim() ?? "",
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

            var currentUpdate = await context.ServiceStatusUpdates
                .AsNoTracking()
                .Where(x => x.ServiceId == serviceId && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                .OrderByDescending(x => x.EffectiveFrom)
                .ThenByDescending(x => x.UpdatedAt)
                .Select(x => new ServiceStatusUpdateSummary(
                    x.Id,
                    x.ClosureState,
                    x.EffectiveFrom,
                    x.EffectiveTo,
                    x.Message,
                    true))
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (currentUpdate is not null)
            {
                return currentUpdate;
            }

            return await context.ServiceStatusUpdates
                .AsNoTracking()
                .Where(x => x.ServiceId == serviceId && x.EffectiveFrom > now)
                .OrderBy(x => x.EffectiveFrom)
                .ThenByDescending(x => x.UpdatedAt)
                .Select(x => new ServiceStatusUpdateSummary(
                    x.Id,
                    x.ClosureState,
                    x.EffectiveFrom,
                    x.EffectiveTo,
                    x.Message,
                    false))
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
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
    }
}
