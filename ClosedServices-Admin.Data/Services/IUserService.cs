using Microsoft.Graph.Models;

namespace ClosedServices_Admin.Data.Services
{
    public interface IUserService
    {
        Task<User?> GetUser(string id);
        Task<IList<User>> GetUsers();
        Task<IList<string>> GetActiveUserIds();
        Task<IList<User>> GetUsers(string[] userIds);
    }
}