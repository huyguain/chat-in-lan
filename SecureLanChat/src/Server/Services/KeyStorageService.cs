using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.Security.Cryptography;

namespace SecureLanChat.Services
{
    public class KeyStorageService : IKeyStorageService
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<KeyStorageService> _logger;
        private readonly ILoggingService _loggingService;

        public KeyStorageService(
            ChatDbContext context,
            ILogger<KeyStorageService> logger,
            ILoggingService loggingService)
        {
            _context = context;
            _logger = logger;
            _loggingService = loggingService;
        }

        public async Task<string> StoreUserPublicKeyAsync(string userId, string publicKey)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                
                if (string.IsNullOrEmpty(publicKey))
                    throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));

                _logger.LogInformation("Storing public key for user {UserId}", userId);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                user.PublicKey = publicKey;
                await _context.SaveChangesAsync();

                _loggingService.LogSecurityEvent("key_storage", userId, "Public key stored successfully", "INFO");
                _logger.LogInformation("Successfully stored public key for user {UserId}", userId);

                return publicKey;
            }
            catch (Exception ex) when (!(ex is UserNotFoundException))
            {
                _loggingService.LogSecurityEvent("key_storage", userId, $"Failed to store public key: {ex.Message}", "ERROR");
                _logger.LogError(ex, "Failed to store public key for user {UserId}", userId);
                throw new DatabaseException("Failed to store public key", ex);
            }
        }

        public async Task<string> GetUserPublicKeyAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Retrieving public key for user {UserId}", userId);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                if (string.IsNullOrEmpty(user.PublicKey))
                {
                    throw new EncryptionException($"No public key found for user {userId}");
                }

                _logger.LogDebug("Successfully retrieved public key for user {UserId}", userId);
                return user.PublicKey;
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is EncryptionException))
            {
                _logger.LogError(ex, "Failed to retrieve public key for user {UserId}", userId);
                throw new DatabaseException("Failed to retrieve public key", ex);
            }
        }

        public async Task<string> StoreSessionKeyAsync(string userId, string connectionId, string aesKey)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                
                if (string.IsNullOrEmpty(connectionId))
                    throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));

                _logger.LogInformation("Storing session key for user {UserId}, connection {ConnectionId}", userId, connectionId);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                // Check if session already exists
                var existingSession = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.UserId.ToString() == userId && s.ConnectionId == connectionId);

                if (existingSession != null)
                {
                    // Update existing session
                    existingSession.AESKey = aesKey;
                    existingSession.ExpiresAt = DateTime.UtcNow.AddHours(24);
                    existingSession.IsActive = true;
                }
                else
                {
                    // Create new session
                    var session = new Session
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        ConnectionId = connectionId,
                        AESKey = aesKey,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddHours(24),
                        IsActive = true
                    };

                    _context.Sessions.Add(session);
                }

                await _context.SaveChangesAsync();

                _loggingService.LogSecurityEvent("session_key_storage", userId, "Session key stored successfully", "INFO");
                _logger.LogInformation("Successfully stored session key for user {UserId}, connection {ConnectionId}", userId, connectionId);

                return aesKey;
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is ArgumentException))
            {
                _loggingService.LogSecurityEvent("session_key_storage", userId, $"Failed to store session key: {ex.Message}", "ERROR");
                _logger.LogError(ex, "Failed to store session key for user {UserId}", userId);
                throw new DatabaseException("Failed to store session key", ex);
            }
        }

        public async Task<string> GetSessionKeyAsync(string userId, string connectionId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                
                if (string.IsNullOrEmpty(connectionId))
                    throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

                _logger.LogDebug("Retrieving session key for user {UserId}, connection {ConnectionId}", userId, connectionId);

                var session = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.UserId.ToString() == userId && 
                                            s.ConnectionId == connectionId && 
                                            s.IsActive && 
                                            s.ExpiresAt > DateTime.UtcNow);

                if (session == null)
                {
                    throw new SessionExpiredException();
                }

                _logger.LogDebug("Successfully retrieved session key for user {UserId}, connection {ConnectionId}", userId, connectionId);
                return session.AESKey;
            }
            catch (Exception ex) when (!(ex is SessionExpiredException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to retrieve session key for user {UserId}", userId);
                throw new DatabaseException("Failed to retrieve session key", ex);
            }
        }

        public async Task<bool> ValidateStoredKeyAsync(string userId, string keyType)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return false;

                _logger.LogDebug("Validating stored key for user {UserId}, type {KeyType}", userId, keyType);

                switch (keyType.ToLower())
                {
                    case "public":
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                        return user != null && !string.IsNullOrEmpty(user.PublicKey);
                    
                    case "session":
                        var session = await _context.Sessions
                            .FirstOrDefaultAsync(s => s.UserId.ToString() == userId && 
                                                    s.IsActive && 
                                                    s.ExpiresAt > DateTime.UtcNow);
                        return session != null && !string.IsNullOrEmpty(session.AESKey);
                    
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate stored key for user {UserId}", userId);
                return false;
            }
        }

        public async Task CleanupExpiredKeysAsync()
        {
            try
            {
                _logger.LogInformation("Starting cleanup of expired keys");

                var expiredSessions = await _context.Sessions
                    .Where(s => s.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                if (expiredSessions.Any())
                {
                    _context.Sessions.RemoveRange(expiredSessions);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
                    _loggingService.LogSecurityEvent("key_cleanup", "system", $"Cleaned up {expiredSessions.Count} expired sessions", "INFO");
                }
                else
                {
                    _logger.LogDebug("No expired sessions found for cleanup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired keys");
                _loggingService.LogSecurityEvent("key_cleanup", "system", $"Failed to cleanup expired keys: {ex.Message}", "ERROR");
            }
        }

        public async Task<bool> KeyExistsAsync(string userId, string keyType)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return false;

                _logger.LogDebug("Checking if key exists for user {UserId}, type {KeyType}", userId, keyType);

                switch (keyType.ToLower())
                {
                    case "public":
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                        return user != null && !string.IsNullOrEmpty(user.PublicKey);
                    
                    case "session":
                        var session = await _context.Sessions
                            .FirstOrDefaultAsync(s => s.UserId.ToString() == userId && s.IsActive);
                        return session != null;
                    
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if key exists for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<Session>> GetAllActiveSessionsAsync()
        {
            try
            {
                _logger.LogDebug("Retrieving all active sessions");

                var sessions = await _context.Sessions
                    .Where(s => s.IsActive && s.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} active sessions", sessions.Count);
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve all active sessions");
                throw new DatabaseException("Failed to retrieve active sessions", ex);
            }
        }

        public async Task<List<Session>> GetActiveSessionsByUserIdAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Retrieving active sessions for user {UserId}", userId);

                var sessions = await _context.Sessions
                    .Where(s => s.UserId.ToString() == userId && 
                               s.IsActive && 
                               s.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} active sessions for user {UserId}", sessions.Count, userId);
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve active sessions for user {UserId}", userId);
                throw new DatabaseException("Failed to retrieve active sessions for user", ex);
            }
        }
    }
}
