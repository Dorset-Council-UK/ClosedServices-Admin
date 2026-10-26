using Microsoft.Graph.Models;

namespace ClosedServices_Admin.Data.Services
{
    public interface IUserService
    {
        Task<User?> GetUser(string id, CancellationToken ct = default);
        Task<IReadOnlyCollection<ClosedServices_Admin.Data.Models.UserServicePermission>> GetUserPermissions(string userId, CancellationToken ct = default);
        Task SaveUserPermissions(string userId, IEnumerable<Guid> serviceTypeIds, IEnumerable<Guid> serviceIds, CancellationToken ct = default);
        Task<IList<User>> GetUsers();
        Task<IList<string>> GetActiveUserIds();
        Task<IList<User>> GetUsers(string[] userIds);
    }
}