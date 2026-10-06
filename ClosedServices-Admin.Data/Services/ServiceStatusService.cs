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

            var operatingDay = await context.ServiceOperatingDays
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.DayOfWeek == now.ToDateTimeUtc().DayOfWeek, ct)
                .ConfigureAwait(false);

            var time = TimeOnly.FromDateTime(now.ToDateTimeUtc());
            var isOpen = operatingDay is { IsOpen: true }
                && (!operatingDay.OpenTime.HasValue || operatingDay.OpenTime <= time)
                && (!operatingDay.CloseTime.HasValue || operatingDay.CloseTime > time);

            return new(isOpen ? ClosureState.NoDisruption : ClosureState.Closed, false, null, null, null);
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
                Message = string.IsNullOrWhiteSpace(command.Message) ? "No additional information provided." : command.Message.Trim(),
                EffectiveFrom = command.EffectiveFrom,
                EffectiveTo = command.EffectiveTo,
                UpdatedByExternalUserId = command.UpdatedByExternalUserId,
                UpdatedAt = SystemClock.Instance.GetCurrentInstant()
            });

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Created service status update for service {ServiceId}", command.ServiceId);
        }
    }
}
