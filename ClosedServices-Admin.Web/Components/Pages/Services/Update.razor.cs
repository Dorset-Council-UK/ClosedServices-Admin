using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NodaTime;
using System.Security.Claims;

namespace ClosedServices_Admin.Components.Pages.Services
{
    public partial class Update(IServicesService servicesService,
        IServiceStatusService serviceStatusService,
        ILogger<Home> logger,
        NavigationManager navigationManager)
    {
        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationState { get; set; }

        [Parameter]
        public string ServiceTypeRoute { get; set; } = string.Empty;

        [Parameter]
        public Guid ServiceId { get; set; }

        private ServiceType? ParsedServiceType { get; set; }

        private string ServiceTypeDisplayName => ParsedServiceType?.ToString() ?? ServiceTypeRoute;

        private Service? Service { get; set; } = null!;

        private string BackLink => $"services/{ServiceTypeRoute}";
        private bool isLoading = false;
        private ServiceCurrentStatus? CurrentStatus { get; set; }
        private ClosureState SelectedClosureState { get; set; } = ClosureState.NoDisruption;
        private string SelectedDuration { get; set; } = "today";
        private DateOnly? StartDate { get; set; }
        private TimeOnly? StartTime { get; set; }
        private DateOnly? EndDate { get; set; }
        private TimeOnly? EndTime { get; set; }
        private string? Message { get; set; }
        private string? ValidationError { get; set; }
        private bool isSaving;

        protected override async Task OnParametersSetAsync()
        {
            isLoading = true;
            try
            {
                if (string.IsNullOrWhiteSpace(ServiceTypeRoute)
                    || int.TryParse(ServiceTypeRoute, out _)
                    || !Enum.TryParse<ServiceType>(ServiceTypeRoute, ignoreCase: true, out var parsedServiceType)
                    || !Enum.IsDefined(parsedServiceType))
                {
                    navigationManager.NavigateTo("not-found");
                    return;
                }

                ParsedServiceType = parsedServiceType;

                if (AuthenticationState is null)
                {
                    logger.LogWarning("Attempt to access home page without authentication");
                    return;
                }

                var authState = await AuthenticationState;
                if (!authState.User.IsAuthenticated)
                {
                    logger.LogWarning("Attempt to access home page without authentication");
                    return;
                }

                Service = await servicesService.GetServiceForUser(authState.User.UserId, ServiceId);
                if(Service is null)
                {
                    navigationManager.NavigateTo("not-found");
                    return;
                }

                CurrentStatus = await serviceStatusService.GetCurrentStatus(ServiceId, SystemClock.Instance.GetCurrentInstant());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading services for user");
                navigationManager.NavigateTo("error");
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task SaveAsync()
        {
            ValidationError = null;
            var now = SystemClock.Instance.GetCurrentInstant();
            var interval = GetInterval(now);
            if (interval is null)
            {
                return;
            }

            try
            {
                isSaving = true;
                var authState = await AuthenticationState!;
                await serviceStatusService.CreateStatusUpdate(new(ServiceId, SelectedClosureState, Message, interval.Value.Start, interval.Value.End, authState.User.UserId));
                navigationManager.NavigateTo(BackLink);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving status update for service {ServiceId}", ServiceId);
                ValidationError = "Unable to save the status update. Please try again.";
            }
            finally
            {
                isSaving = false;
            }
        }

        private (Instant Start, Instant? End)? GetInterval(Instant now)
        {
            if (SelectedDuration != "custom")
            {
                var endDate = SelectedDuration switch
                {
                    "tomorrow" => DateOnly.FromDateTime(now.ToDateTimeUtc()).AddDays(1),
                    "week" => DateOnly.FromDateTime(now.ToDateTimeUtc()).AddDays(7 - (int)now.ToDateTimeUtc().DayOfWeek),
                    _ => DateOnly.FromDateTime(now.ToDateTimeUtc())
                };
                return (now, AtEndOfDay(endDate, now));
            }

            if (StartDate is null)
            {
                ValidationError = "Enter a start date.";
                return null;
            }

            var start = AtTime(StartDate.Value, StartTime ?? TimeOnly.MinValue, now);
            Instant? end = EndDate is null ? null : AtTime(EndDate.Value, EndTime ?? TimeOnly.MaxValue, now);
            if (end is not null && end <= start)
            {
                ValidationError = "The end date and time must be after the start date and time.";
                return null;
            }

            return (start, end);
        }

        private static Instant AtEndOfDay(DateOnly date, Instant now) => AtTime(date, TimeOnly.MaxValue, now);
        private static Instant AtTime(DateOnly date, TimeOnly time, Instant now)
        {
            var dateTime = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc);
            return Instant.FromDateTimeUtc(dateTime);
        }
    }
}
