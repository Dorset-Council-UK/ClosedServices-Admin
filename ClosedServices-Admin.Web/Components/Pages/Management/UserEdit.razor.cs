using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Graph.Models;

namespace ClosedServices_Admin.Components.Pages.Management
{
    public partial class UserEdit(IUserService userService,
                                  ILogger<UserEdit> logger,
                                  NavigationManager navigationManager)
    {
        [Parameter]
        public required string Id { get; set; }

        private User? User { get; set; }

        private string? ErrorMessage { get; set; } = null;
        private bool IsBusy { get; set; } = false;

        protected override async Task OnParametersSetAsync()
        {
            IsBusy = true;
            User = await userService.GetUser(Id);
            if (User is not null)
            {
                
            }
            else
            {
                navigationManager.NavigateTo("management/users");
            }

            IsBusy = false;
        }

        public async Task OnSubmit()
        {
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                

            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred updating a users permission";
                logger.LogError(ex, "An error occurred updating a users permission");
            }
            finally
            {
                IsBusy = false;
            }

        }

    }
}