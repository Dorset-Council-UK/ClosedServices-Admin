using Azure.Identity;
using ClosedServices_Admin.Data;
using ClosedServices_Admin_Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace ClosedServices_Admin.Data.Services
{
    public class UserService(IDbContextFactory<ApplicationDbContext> contextFactory,
                             IMemoryCache memoryCache,
                             IOptions<ClosedServicesOptions> closedServicesOptions,
                             ILogger<UserService> logger) : IUserService
    {
        public async Task<IList<Microsoft.Graph.Models.User>> GetUsers()
        {
            string cachekey = "AllGraphUsers";
            if (memoryCache.TryGetValue(cachekey, out IList<Microsoft.Graph.Models.User>? cachedUsers) && cachedUsers is not null)
            {
                return cachedUsers;
            }
            List<Microsoft.Graph.Models.User> allUsers = new List<Microsoft.Graph.Models.User>();
            var graphClient = GetGraphClient();
            if (graphClient != null)
            {
                var users = await graphClient.Users.GetAsync();
                if (users != null && users.Value != null)
                {
                    allUsers.AddRange(users.Value);
                    string? skipLink = users.OdataNextLink;
                    bool hasNextPage = !string.IsNullOrEmpty(skipLink);
                    while (hasNextPage)
                    {

                        var nextPage = await graphClient.Users.WithUrl(skipLink).GetAsync();
                        if (nextPage != null && nextPage.Value != null)
                        {
                            allUsers.AddRange(nextPage.Value);
                            skipLink = nextPage.OdataNextLink;
                            hasNextPage = !string.IsNullOrEmpty(skipLink);
                        }
                    }
                }
                else
                {
                    throw new System.Exception("Graph client could not be initialized");
                }
            }
            if (allUsers.Count != 0)
            {
                memoryCache.Set(cachekey, allUsers, System.TimeSpan.FromMinutes(5));
            }
            return allUsers;
        }

        public async Task<Microsoft.Graph.Models.User?> GetUser(string id)
        {
            var graphClient = GetGraphClient();
            if (graphClient is not null)
            {
                var user = await graphClient.Users[id].GetAsync();
                return user;
            }
            return null;
        }

        public async Task<IList<Microsoft.Graph.Models.User>> GetUsers(string[] userIds)
        {
            var graphClient = GetGraphClient();
            if (graphClient is null || userIds == null || userIds.Length == 0)
                return [];

            var batchRequestContent = new Microsoft.Graph.BatchRequestContentCollection(graphClient);
            var requestIds = new Dictionary<string, string>();

            foreach (var userId in userIds)
            {
                var request = graphClient.Users[userId].ToGetRequestInformation();
                var reqId = await batchRequestContent.AddBatchRequestStepAsync(request);
                requestIds[reqId] = userId;
            }

            var response = await graphClient.Batch.PostAsync(batchRequestContent);
            var users = new List<Microsoft.Graph.Models.User>();

            foreach (var request in requestIds)
            {
                try
                {
                    var user = await response.GetResponseByIdAsync<Microsoft.Graph.Models.User>(request.Key);
                    users.Add(user);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error retrieving user ID {userId} as part of a collection", request.Value);
                }
            }

            return users;
        }

        private GraphServiceClient? GetGraphClient()
        {
            AzureAdOptions AzureAdOptions = closedServicesOptions.Value.AzureAd;
            if (!string.IsNullOrEmpty(AzureAdOptions.ClientId))
            {
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var tenantId = AzureAdOptions.TenantId;
                var clientId = AzureAdOptions.ClientId;
                var clientSecret = AzureAdOptions.ClientSecret;

                var options = new TokenCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };

                // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
                var clientSecretCredential = new ClientSecretCredential(
                    tenantId, clientId, clientSecret, options);

                var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

                return graphClient;
            }
            return null;
        }

        public async Task<IList<string>> GetActiveUserIds()
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            return await context.UserServicePermissions.Select(u => u.ExternalUserId).AsNoTracking().ToListAsync();
        }

        

        //public async Task<IList<Microsoft.Graph.Models.User>> GetUsersInRole(string roleName)
        //{
        //    await using var context = await contextFactory.CreateDbContextAsync();
        //    var usersInRole = await context.ApplicationUserRoles
        //        .Where(r => r.Role!.RoleName == roleName)
        //        .Select(r => r.UserId)
        //        .ToListAsync();

        //    var users = await GetUsers([.. usersInRole]);

        //    return users;
        //}

        
        //public async Task<bool> UpdateUserRoles(string userId, ICollection<int> SelectedUserRoleIds, CancellationToken ct = default)
        //{
        //    var existingUserRoles = await GetApplicationUserRoles(userId);

        //    // Determine the problem types to remove
        //    var userRolesToRemove = existingUserRoles
        //        .Where(r => !SelectedUserRoleIds.Contains(r.ApplicationRoleId))
        //        .ToList();

        //    await using var context = await contextFactory.CreateDbContextAsync(ct);

        //    // Remove the entities
        //    context.ApplicationUserRoles.RemoveRange(userRolesToRemove);

        //    // Determine the roles to add
        //    var userRolesToAdd = SelectedUserRoleIds
        //        .Where(selectedRoleId => !existingUserRoles.Exists(c => c.ApplicationRoleId == selectedRoleId))
        //        .Select(selectedRoleId => new ApplicationUserRole { ApplicationRoleId = selectedRoleId, UserId = userId })
        //        .ToList();

        //    // Add the new problem types
        //    context.ApplicationUserRoles.AddRange(userRolesToAdd);

        //    // Mark entities as unchanged if they haven't actually changed
        //    foreach (var existingUserRole in existingUserRoles.Where(existingUserRole => SelectedUserRoleIds.Contains(existingUserRole.ApplicationRoleId)))
        //    {
        //        context.Entry(existingUserRole).State = EntityState.Unchanged;
        //    }
        //    await context.SaveChangesAsync(ct).ConfigureAwait(false);
        //    return true;
        //}
    }
}
