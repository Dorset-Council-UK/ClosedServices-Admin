using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using GdsBlazorComponents;
using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using NodaTime;
using System.Security.Claims;

namespace ClosedServices_Admin.Components.Pages.Services
{
    public partial class Update(IServicesService servicesService,
        IServiceStatusService serviceStatusService,
        IServiceOperatingHoursService operatingHoursService,
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
        private ServiceOperatingHoursEvaluation? OperatingHoursEvaluation { get; set; }
        private UpdateServiceStatusFormModel FormModel { get; } = new();
        private EditContext FormEditContext { get; set; } = null!;
        private string CurrentStatusDayLabel { get; set; } = "Today";
        private string NextOperatingDayLabel { get; set; } = "Tomorrow";
        private bool ShowTodayOption { get; set; }
        private bool ShowTodayAndNextOption { get; set; }
        private bool ShowTomorrowOption { get; set; }
        private ServiceStatusUpdateSummary? RemovableClosure { get; set; }
        private bool ShowRemovalSuccessMessage { get; set; }
        private string? SaveErrorMessage { get; set; }
        private bool isSaving;

        private string RemovalConfirmationLink => RemovableClosure is null
            ? string.Empty
            : $"services/{ServiceTypeRoute}/update/{ServiceId}/remove-closure/{RemovableClosure.Id}";

        private string ClosureStatusText => RemovableClosure is null
            ? string.Empty
            : RemovableClosure.IsCurrent
                ? $"This {ServiceTypeDisplayName.Singularize().ToLowerInvariant()} is currently {FormatClosureState(RemovableClosure.ClosureState)}"
                : $"This {ServiceTypeDisplayName.Singularize().ToLowerInvariant()} has an upcoming closure on {FormatClosureDate(RemovableClosure.EffectiveFrom)}";

        protected override void OnInitialized()
        {
            FormEditContext = new EditContext(FormModel);
            FormEditContext.SetFieldCssClassProvider(new GdsFieldCssClassProvider());
        }

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

                var now = SystemClock.Instance.GetCurrentInstant();
                ShowRemovalSuccessMessage = IsRemovalSuccessMessageRequested();
                OperatingHoursEvaluation = operatingHoursService.Evaluate(Service, now);

                ConfigureDurationOptions(now);

                var statusInstant = OperatingHoursEvaluation.IsWithinTodayOperatingHours
                    ? now
                    : OperatingHoursEvaluation.NextOperatingPeriod?.OpenInstant ?? now;

                CurrentStatus = await serviceStatusService.GetCurrentStatus(ServiceId, statusInstant);
                RemovableClosure = await serviceStatusService.GetCurrentOrNextStatusUpdate(ServiceId, now);
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
            SaveErrorMessage = null;

            var now = SystemClock.Instance.GetCurrentInstant();
            var interval = GetInterval(now);
            if (interval is null)
            {
                SaveErrorMessage = "Unable to determine a valid closure period from the values entered. Please review your dates and times and try again.";
                return;
            }

            try
            {
                isSaving = true;
                var authState = await AuthenticationState!;
                await serviceStatusService.CreateStatusUpdate(new(ServiceId, FormModel.SelectedClosureState, FormModel.Message, interval.Value.Start, interval.Value.End, authState.User.UserId));
                navigationManager.NavigateTo(BackLink);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving status update for service {ServiceId}", ServiceId);
                SaveErrorMessage = "Unable to save the status update. Please try again.";
            }
            finally
            {
                isSaving = false;
            }
        }

        private (Instant Start, Instant? End)? GetInterval(Instant now)
        {
            if (FormModel.SelectedDuration != "custom")
            {
                if (OperatingHoursEvaluation is null)
                {
                    return null;
                }

                return FormModel.SelectedDuration switch
                {
                    "today" when ShowTodayOption && OperatingHoursEvaluation.TodayPeriod is not null
                        => (now, OperatingHoursEvaluation.TodayPeriod.CloseInstant),
                    "today-and-next"
                        when ShowTodayAndNextOption && OperatingHoursEvaluation.NextOperatingPeriodAfterToday is not null
                        => (now, OperatingHoursEvaluation.NextOperatingPeriodAfterToday.CloseInstant),
                    "tomorrow"
                        when ShowTomorrowOption && OperatingHoursEvaluation.NextOperatingPeriodAfterToday is not null
                        => (OperatingHoursEvaluation.NextOperatingPeriodAfterToday.OpenInstant, OperatingHoursEvaluation.NextOperatingPeriodAfterToday.CloseInstant),
                    _ => InvalidPresetSelection()
                };
            }

            if (FormModel.StartDate is null)
            {
                return null;
            }

            var start = AtTime(FormModel.StartDate.Value, FormModel.StartTime ?? TimeOnly.MinValue);
            Instant? end = FormModel.EndDate is null ? null : AtTime(FormModel.EndDate.Value, FormModel.EndTime ?? TimeOnly.MaxValue);
            if (end is not null && end <= start)
            {
                return null;
            }

            return (start, end);
        }

        private void ConfigureDurationOptions(Instant now)
        {
            if (OperatingHoursEvaluation is null)
            {
                FormModel.SelectedDuration = "custom";
                return;
            }

            var localNowDate = OperatingHoursEvaluation.LocalNow.Date;

            ShowTodayOption = OperatingHoursEvaluation.IsTodayOperationalBeforeClose;
            ShowTodayAndNextOption = ShowTodayOption && OperatingHoursEvaluation.NextOperatingPeriodAfterToday is not null;
            ShowTomorrowOption = OperatingHoursEvaluation.NextOperatingPeriodAfterToday is not null;

            if (OperatingHoursEvaluation.IsWithinTodayOperatingHours)
            {
                CurrentStatusDayLabel = "Today";
            }
            else
            {
                var statusTargetDate = OperatingHoursEvaluation.NextOperatingPeriod?.Date ?? localNowDate;
                CurrentStatusDayLabel = operatingHoursService.FormatOperatingDayLabel(statusTargetDate, localNowDate);
            }

            if (OperatingHoursEvaluation.NextOperatingPeriodAfterToday is not null)
            {
                NextOperatingDayLabel = operatingHoursService.FormatOperatingDayLabel(
                    OperatingHoursEvaluation.NextOperatingPeriodAfterToday.Date,
                    localNowDate);
            }

            FormModel.SelectedDuration = ShowTodayOption
                ? "today"
                : ShowTomorrowOption
                    ? "tomorrow"
                    : "custom";
        }

        private (Instant Start, Instant? End)? InvalidPresetSelection()
        {
            return null;
        }

        private static Instant AtTime(DateOnly date, TimeOnly time)
        {
            var zone = DateTimeZoneProviders.Tzdb["Europe/London"];
            var localDateTime = LocalDateTime.FromDateTime(date.ToDateTime(time));
            return localDateTime.InZoneLeniently(zone).ToInstant();
        }

        private static string FormatClosureState(ClosureState closureState)
        {
            return closureState switch
            {
                ClosureState.PartiallyClosed => "partially closed",
                ClosureState.Closed => "closed",
                _ => closureState.ToString()
            };
        }

        private static string FormatClosureDate(Instant instant)
        {
            var zone = DateTimeZoneProviders.Tzdb["Europe/London"];
            var closureDate = instant.InZone(zone).Date;
            var localDateTime = closureDate.AtMidnight();
            return localDateTime.ToString("dddd d MMMM yyyy", null);
        }

        private bool IsRemovalSuccessMessageRequested()
        {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
            if (!QueryHelpers.ParseQuery(uri.Query).TryGetValue("removed", out var removed))
            {
                return false;
            }

            return string.Equals(removed.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        }

    }
}
