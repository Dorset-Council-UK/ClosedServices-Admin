using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;

namespace ClosedServices_Admin.Components.Pages.Management
{
    public partial class UserEdit(IUserService userService,
                                  IServicesService servicesService,
                                  ILogger<UserEdit> logger,
                                  NavigationManager navigationManager)
    {
        [Parameter]
        public required string Id { get; set; }

        private User? User { get; set; }

        private string? ErrorMessage { get; set; } = null;
        private bool IsBusy { get; set; } = false;
        private UserPermissionSelection? Selection { get; set; }
        private string? loadedUserId;

        protected override async Task OnInitializedAsync()
        {
            IsBusy = true;
            try
            {
                var user = await userService.GetUser(Id);
                if (user is null)
                {
                    navigationManager.NavigateTo("management/users");
                    return;
                }
                var userId = user.Id ?? Id;
                var types = await servicesService.GetServiceTypes();
                var services = await servicesService.GetAllServices();
                var grants = await userService.GetUserPermissions(userId);
                Selection = new UserPermissionSelection(types, services, grants);
                User = user;
                loadedUserId = userId;
            }
            catch (ApiException ex) when (ex.ResponseStatusCode == 404)
            {
                navigationManager.NavigateTo("management/users");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load this user's permissions. Reload the page to try again.";
                logger.LogError(ex, "Unable to load permissions for user {UserId}", Id);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnTypeChanged(ServiceTypePermissionSelection group, bool selected)
        {
            if (!IsBusy)
            {
                group.SetAllServices(selected);
            }
        }

        private void OnServiceChanged(ServiceTypePermissionSelection group, ServicePermissionSelection service, bool selected)
        {
            if (!IsBusy)
            {
                group.SetService(service, selected);
            }
        }

        public async Task OnSubmit()
        {
            if (IsBusy || Selection is null || User is null || loadedUserId is null)
            {
                return;
            }

            var userId = loadedUserId;
            var typeIds = Selection.SelectedTypeIds.ToArray();
            var serviceIds = Selection.SelectedServiceIds.ToArray();
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await userService.SaveUserPermissions(userId, typeIds, serviceIds);
                navigationManager.NavigateTo("management/users");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to save this user's permissions. Your selections have been kept. Please try again.";
                logger.LogError(ex, "Unable to save permissions for user {UserId}", userId);
            }
            finally
            {
                IsBusy = false;
            }

        }

        private void Cancel()
        {
            if (!IsBusy)
            {
                navigationManager.NavigateTo("management/users");
            }
        }

    }
}