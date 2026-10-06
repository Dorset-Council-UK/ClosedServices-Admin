using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NodaTime;
using System.Globalization;
using System.Security.Claims;

namespace ClosedServices_Admin.Components.Pages.Services
{
    public partial class RemoveClosure(IServicesService servicesService,
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

        [Parameter]
        public Guid StatusUpdateId { get; set; }

        private Service? Service { get; set; }

        private ServiceStatusUpdateSummary? ClosureToRemove { get; set; }

        private string? ValidationError { get; set; }

        private bool isLoading;

        private bool isDeleting;

        private string UpdatePageLink => $"services/{ServiceTypeRoute}/update/{ServiceId}";

        private string ClosureStateText => ClosureToRemove is null ? string.Empty : FormatClosureState(ClosureToRemove.ClosureState);

        private string EffectiveFromText => ClosureToRemove is null ? string.Empty : FormatDateTime(ClosureToRemove.EffectiveFrom);

        private string EffectiveToText => ClosureToRemove?.EffectiveTo is null ? "Not set" : FormatDateTime(ClosureToRemove.EffectiveTo.Value);

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

                if (AuthenticationState is null)
                {
                    logger.LogWarning("Attempt to access remove closure page without authentication");
                    return;
                }

                var authState = await AuthenticationState;
                if (!authState.User.IsAuthenticated)
                {
                    logger.LogWarning("Attempt to access remove closure page without authentication");
                    return;
                }

                Service = await servicesService.GetServiceForUser(authState.User.UserId, ServiceId);
                if (Service is null)
                {
                    navigationManager.NavigateTo("not-found");
                    return;
                }

                var now = SystemClock.Instance.GetCurrentInstant();
                var candidateClosure = await serviceStatusService.GetCurrentOrNextStatusUpdate(ServiceId, now);
                if (candidateClosure is null || candidateClosure.Id != StatusUpdateId)
                {
                    navigationManager.NavigateTo("not-found");
                    return;
                }

                ClosureToRemove = candidateClosure;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading closure removal page for service {ServiceId}", ServiceId);
                navigationManager.NavigateTo("error");
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task ConfirmDeleteAsync()
        {
            ValidationError = null;

            if (ClosureToRemove is null)
            {
                navigationManager.NavigateTo("not-found");
                return;
            }

            try
            {
                isDeleting = true;
                var deleted = await serviceStatusService.DeleteStatusUpdate(ServiceId, StatusUpdateId);
                if (!deleted)
                {
                    navigationManager.NavigateTo("not-found");
                    return;
                }

                navigationManager.NavigateTo($"{UpdatePageLink}?removed=true");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting service status update {StatusUpdateId}", StatusUpdateId);
                ValidationError = "Unable to remove this closure. Please try again.";
            }
            finally
            {
                isDeleting = false;
            }
        }

        private Task CancelAsync()
        {
            navigationManager.NavigateTo(UpdatePageLink);
            return Task.CompletedTask;
        }

        private static string FormatClosureState(ClosureState closureState)
        {
            return closureState switch
            {
                ClosureState.PartiallyClosed => "Partially closed",
                ClosureState.Closed => "Closed",
                _ => closureState.ToString()
            };
        }

        private static string FormatDateTime(Instant instant)
        {
            var zone = DateTimeZoneProviders.Tzdb["Europe/London"];
            var localDateTime = instant.InZone(zone).LocalDateTime;
            return localDateTime.ToDateTimeUnspecified().ToString("dddd d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
