using SecureLanChat.Models;

namespace SecureLanChat.Interfaces
{
    public interface IUserService
    {
        Task<User> LoginAsync(string username);
        Task LogoutAsync(string userId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task UpdateUserStatusAsync(string userId, UserStatus status);
        Task<List<User>> GetOnlineUsersAsync();
        Task<User?> GetUserByIdAsync(string userId);
        Task<bool> ValidateUsernameAsync(string username);
    }
}
