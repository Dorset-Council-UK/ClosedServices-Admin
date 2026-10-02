using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace ClosedServices_Admin.Components.Pages.Services
{
    public partial class Index(IServicesService servicesService,
        ILogger<Home> logger,
        NavigationManager navigationManager)
    {
        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationState { get; set; }

        [Parameter]
        public string ServiceTypeRoute { get; set; } = string.Empty;

        private ServiceType? ParsedServiceType { get; set; }

        private string ServiceTypeDisplayName => ParsedServiceType?.ToString() ?? ServiceTypeRoute;

        private IReadOnlyCollection<Service> Services { get; set; } = Array.Empty<Service>();

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrWhiteSpace(ServiceTypeRoute)
                || int.TryParse(ServiceTypeRoute, out _)
                || !Enum.TryParse<ServiceType>(ServiceTypeRoute, ignoreCase: true, out var parsedServiceType)
                || !Enum.IsDefined(parsedServiceType))
            {
                navigationManager.NavigateTo("/not-found");
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

            Services = await servicesService.GetServicesForUserByType(authState.User.UserId, parsedServiceType);

        }
    }
}