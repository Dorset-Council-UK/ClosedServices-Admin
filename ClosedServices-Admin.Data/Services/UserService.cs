using Azure.Identity;
using ClosedServices_Admin.Data;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.Components.Authorization;
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
                             ILogger<UserService> logger,
                             AuthenticationStateProvider authenticationStateProvider) : IUserService
    {
        public async Task<IList<Microsoft.Graph.Models.User>> GetUsers()
        {
            await EnsureAdminAsync();
            string cachekey = "AllGraphUsers";
            if (memoryCache.TryGetValue(cachekey, out IList<Microsoft.Graph.Models.User>? cachedUsers) && cachedUsers is not null)
            {
                return cachedUsers;
            }
            List<Microsoft.Graph.Models.User> allUsers = [];
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

        public async Task<Microsoft.Graph.Models.User?> GetUser(string id, CancellationToken ct = default)
        {
            await EnsureAdminAsync();
            var graphClient = GetGraphClient();
            if (graphClient is not null)
            {
                var user = await graphClient.Users[id].GetAsync(cancellationToken: ct);
                return user;
            }
            return null;
        }

        public async Task<IReadOnlyCollection<Models.UserServicePermission>> GetUserPermissions(string userId, CancellationToken ct = default)
        {
            await EnsureAdminAsync();
            ValidateUserId(userId);
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.UserServicePermissions
                .AsNoTracking()
                .Where(permission => permission.ExternalUserId == userId)
                .ToListAsync(ct);
        }

        public async Task SaveUserPermissions(string userId, IEnumerable<Guid> serviceTypeIds, IEnumerable<Guid> serviceIds, CancellationToken ct = default)
        {
            await EnsureAdminAsync();
            ValidateUserId(userId);
            ArgumentNullException.ThrowIfNull(serviceTypeIds);
            ArgumentNullException.ThrowIfNull(serviceIds);
            var selectedTypes = serviceTypeIds.ToHashSet();
            var selectedServices = serviceIds.ToHashSet();

            await using var context = await contextFactory.CreateDbContextAsync(ct);
            var validTypes = await context.ServiceTypes
                .AsNoTracking()
                .Where(type => selectedTypes.Contains(type.Id))
                .Select(type => type.Id)
                .ToListAsync(ct);
            var validServices = await context.Services
                .AsNoTracking()
                .Where(service => selectedServices.Contains(service.Id))
                .Select(service => new { service.Id, service.ServiceTypeId })
                .ToListAsync(ct);

            if (selectedTypes.Contains(Guid.Empty) || validTypes.Count != selectedTypes.Count)
            {
                throw new ArgumentException("One or more service types no longer exist.", nameof(serviceTypeIds));
            }
            if (selectedServices.Contains(Guid.Empty) || validServices.Count != selectedServices.Count)
            {
                throw new ArgumentException("One or more services no longer exist.", nameof(serviceIds));
            }

            var remaining = new HashSet<(Guid? ServiceId, Guid? ServiceTypeId)>();
            foreach (var typeId in selectedTypes)
            {
                remaining.Add((null, typeId));
            }
            foreach (var service in validServices.Where(service => !selectedTypes.Contains(service.ServiceTypeId)))
            {
                remaining.Add((service.Id, null));
            }

            var existing = await context.UserServicePermissions
                .Where(permission => permission.ExternalUserId == userId)
                .ToListAsync(ct);
            foreach (var permission in existing)
            {
                if (!remaining.Remove((permission.ServiceId, permission.ServiceTypeId)))
                {
                    context.UserServicePermissions.Remove(permission);
                }
            }
            foreach (var target in remaining)
            {
                context.UserServicePermissions.Add(new Models.UserServicePermission
                {
                    Id = Guid.NewGuid(),
                    ExternalUserId = userId,
                    ServiceId = target.ServiceId,
                    ServiceTypeId = target.ServiceTypeId
                });
            }

            await context.SaveChangesAsync(ct);
        }

        private async Task EnsureAdminAsync()
        {
            var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated != true || !authState.User.IsInRole("Admin"))
            {
                throw new UnauthorizedAccessException("The Admin role is required.");
            }
        }

        private static void ValidateUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || userId.Length > 200)
            {
                throw new ArgumentException("A user ID of at most 200 characters is required.", nameof(userId));
            }
        }

        public async Task<IList<Microsoft.Graph.Models.User>> GetUsers(string[] userIds)
        {
            await EnsureAdminAsync();
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
            await EnsureAdminAsync();
            await using var context = await contextFactory.CreateDbContextAsync();
            return await context.UserServicePermissions.Select(u => u.ExternalUserId).AsNoTracking().ToListAsync();
        }
    }
}
