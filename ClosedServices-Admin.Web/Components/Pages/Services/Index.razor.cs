using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Services;
using GdsBlazorComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NodaTime;
using System.Security.Claims;

namespace ClosedServices_Admin.Components.Pages.Services
{
    public partial class Index(IServicesService servicesService,
        IServiceStatusService serviceStatusService,
        ILogger<Home> logger,
        NavigationManager navigationManager)
    {
        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationState { get; set; }

        [Parameter]
        public string ServiceTypeRoute { get; set; } = string.Empty;

        private ServiceType? ParsedServiceType { get; set; }

        private string ServiceTypeDisplayName => ParsedServiceType?.ToString() ?? ServiceTypeRoute;

        private IReadOnlyCollection<ServiceStatusOverview> ServiceOverviews { get; set; } = [];

        private bool isLoading = false;

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

                var services = await servicesService.GetServicesForUserByType(authState.User.UserId, parsedServiceType);
                var now = SystemClock.Instance.GetCurrentInstant();
                ServiceOverviews = await serviceStatusService.GetStatusOverviews([.. services.Select(service => service.Id)], now);
            }catch(Exception ex)
            {
                logger.LogError(ex, "Error loading services for user");
                navigationManager.NavigateTo("error");
            }
            finally
            {
                isLoading = false;
            }
        }

        private static string GetStatusText(ClosureState closureState)
        {
            return closureState switch
            {
                ClosureState.Closed => "Closed",
                ClosureState.PartiallyClosed => "Partially closed",
                _ => "No disruption",
            };
        }

        private static GdsTagColour GetStatusTagColour(ClosureState closureState)
        {
            return closureState switch
            {
                ClosureState.Closed => GdsTagColour.Red,
                ClosureState.PartiallyClosed => GdsTagColour.Yellow,
                _ => GdsTagColour.Green,
            };
        }
    }
}