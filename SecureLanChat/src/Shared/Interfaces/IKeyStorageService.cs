using SecureLanChat.Models;

namespace SecureLanChat.Interfaces
{
    public interface IKeyStorageService
    {
        Task<string> StoreUserPublicKeyAsync(string userId, string publicKey);
        Task<string> GetUserPublicKeyAsync(string userId);
        Task<string> StoreSessionKeyAsync(string userId, string connectionId, string aesKey);
        Task<string> GetSessionKeyAsync(string userId, string connectionId);
        Task<bool> ValidateStoredKeyAsync(string userId, string keyType);
        Task CleanupExpiredKeysAsync();
        Task<bool> KeyExistsAsync(string userId, string keyType);
        Task<List<Session>> GetAllActiveSessionsAsync();
        Task<List<Session>> GetActiveSessionsByUserIdAsync(string userId);
    }
}

