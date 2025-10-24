using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace SecureLanChat.Services
{
    public class UserService : IUserService
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly ILoggingService _loggingService;
        private readonly IKeyStorageService _keyStorageService;

        public UserService(
            ChatDbContext context,
            ILogger<UserService> logger,
            ILoggingService loggingService,
            IKeyStorageService keyStorageService)
        {
            _context = context;
            _logger = logger;
            _loggingService = loggingService;
            _keyStorageService = keyStorageService;
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                    throw new ArgumentException("Username cannot be null or empty", nameof(username));
                
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password cannot be null or empty", nameof(password));

                _logger.LogInformation("Attempting login for user {Username}", username);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                {
                    _loggingService.LogSecurityEvent("failed_login", username, "User not found", "WARNING");
                    throw new UserNotFoundException(username);
                }

                // Verify password (in real implementation, use proper password hashing)
                if (!VerifyPassword(password, user.PasswordHash))
                {
                    _loggingService.LogSecurityEvent("failed_login", username, "Invalid password", "WARNING");
                    throw new InvalidCredentialsException();
                }

                // Update user status
                user.IsOnline = true;
                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _loggingService.LogUserAction(user.Id.ToString(), "login", "User logged in successfully");
                _logger.LogInformation("User {Username} logged in successfully", username);

                return user;
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is InvalidCredentialsException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to login user {Username}", username);
                throw new DatabaseException("Failed to login user", ex);
            }
        }

        public async Task<User> RegisterAsync(string username, string password, string email = null)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                    throw new ArgumentException("Username cannot be null or empty", nameof(username));
                
                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password cannot be null or empty", nameof(password));

                _logger.LogInformation("Attempting registration for user {Username}", username);

                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (existingUser != null)
                {
                    _loggingService.LogSecurityEvent("registration_failed", username, "Username already exists", "WARNING");
                    throw new UsernameAlreadyExistsException(username);
                }

                // Create new user
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = HashPassword(password),
                    Email = email,
                    IsOnline = false,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _loggingService.LogUserAction(user.Id.ToString(), "register", "User registered successfully");
                _logger.LogInformation("User {Username} registered successfully", username);

                return user;
            }
            catch (Exception ex) when (!(ex is UsernameAlreadyExistsException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to register user {Username}", username);
                throw new DatabaseException("Failed to register user", ex);
            }
        }

        public async Task LogoutAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogInformation("Logging out user {UserId}", userId);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                user.IsOnline = false;
                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _loggingService.LogUserAction(userId, "logout", "User logged out successfully");
                _logger.LogInformation("User {UserId} logged out successfully", userId);
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to logout user {UserId}", userId);
                throw new DatabaseException("Failed to logout user", ex);
            }
        }

        public async Task<List<User>> GetOnlineUsersAsync()
        {
            try
            {
                _logger.LogDebug("Retrieving online users");

                var onlineUsers = await _context.Users
                    .Where(u => u.IsOnline)
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} online users", onlineUsers.Count);
                return onlineUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve online users");
                throw new DatabaseException("Failed to retrieve online users", ex);
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                _logger.LogDebug("Retrieving all users");

                var users = await _context.Users
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} total users", users.Count);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve all users");
                throw new DatabaseException("Failed to retrieve all users", ex);
            }
        }

        public async Task<User> GetUserByIdAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Retrieving user {UserId}", userId);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                return user;
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to retrieve user {UserId}", userId);
                throw new DatabaseException("Failed to retrieve user", ex);
            }
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                    throw new ArgumentException("Username cannot be null or empty", nameof(username));

                _logger.LogDebug("Retrieving user by username {Username}", username);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                {
                    throw new UserNotFoundException(username);
                }

                return user;
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to retrieve user by username {Username}", username);
                throw new DatabaseException("Failed to retrieve user by username", ex);
            }
        }

        public async Task UpdateUserStatusAsync(string userId, bool isOnline)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Updating user status for {UserId} to {Status}", userId, isOnline);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                user.IsOnline = isOnline;
                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _loggingService.LogUserAction(userId, "status_update", $"User status updated to {(isOnline ? "online" : "offline")}");
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to update user status for {UserId}", userId);
                throw new DatabaseException("Failed to update user status", ex);
            }
        }

        public async Task<bool> ValidateUserAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return false;

                _logger.LogDebug("Validating user {UserId}", userId);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                return user != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> IsUserOnlineAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return false;

                _logger.LogDebug("Checking if user {UserId} is online", userId);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                return user?.IsOnline ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if user {UserId} is online", userId);
                return false;
            }
        }

        public async Task UpdateLastSeenAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Updating last seen for user {UserId}", userId);

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                if (user == null)
                {
                    throw new UserNotFoundException(userId);
                }

                user.LastSeen = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) when (!(ex is UserNotFoundException) && !(ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to update last seen for user {UserId}", userId);
                throw new DatabaseException("Failed to update last seen", ex);
            }
        }

        private string HashPassword(string password)
        {
            // In production, use proper password hashing like bcrypt or Argon2
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "salt"));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            var hashedPassword = HashPassword(password);
            return hashedPassword == hash;
        }
    }
}