using ClosedServices_Admin_Shared.Options;
using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace ClosedServices_Admin.Components.Pages.Management
{
    public partial class Users(IUserService userService,
                               IOptions<ClosedServicesOptions> closedServicesOptions,
                               NavigationManager navigationManager)
    {


        private IList<User>? AllUsers { get; set; }
        private IList<User>? ActiveUsers { get; set; }
        private IList<User>? AvailableUsers { get; set; }

        private bool IsBusy { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            IsBusy = true;

            IList<string> activeUserIds = await userService.GetActiveUserIds();
            AllUsers = await userService.GetUsers();
            ActiveUsers = [.. AllUsers.Where(u => activeUserIds.Contains(u.Id, StringComparer.OrdinalIgnoreCase))];
            AvailableUsers = [.. AllUsers.Where(u => !activeUserIds.Contains(u.Id, StringComparer.OrdinalIgnoreCase))];
            IsBusy = false;
        }

        

    }
}