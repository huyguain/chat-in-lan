using SecureLanChat.Models;

namespace SecureLanChat.Interfaces
{
    public interface IUserService
    {
        Task<User> LoginAsync(string username);
        Task<User> RegisterAsync(string username, string password, string? email = null);
        Task LogoutAsync(string userId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task UpdateUserStatusAsync(string userId, UserStatus status);
        Task UpdateUserStatusAsync(string userId, bool isOnline);
        Task<List<User>> GetOnlineUsersAsync();
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(string userId);
        Task<User> GetUserByUsernameAsync(string username);
        Task<bool> ValidateUsernameAsync(string username);
        Task<bool> ValidateUserAsync(string userId);
        Task UpdateLastSeenAsync(string userId);
    }
}
