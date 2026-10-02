using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace ClosedServices_Admin.Components.Pages
{
    public partial class Home(IServicesService servicesService,
        ILogger<Home> logger)
    {
        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationState { get; set; }

        private IReadOnlyCollection<Service> Services { get; set; } = Array.Empty<Service>();
        protected override async Task OnInitializedAsync()
        {
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

            Services = await servicesService.GetServicesForUser(authState.User.UserId);

        }
    }
}
