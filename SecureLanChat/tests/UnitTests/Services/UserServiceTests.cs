using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SecureLanChat.Data;
using SecureLanChat.Exceptions;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Services;
using Xunit;

namespace SecureLanChat.Tests.Services
{
    public class UserServiceTests : IDisposable
    {
        private readonly ChatDbContext _context;
        private readonly Mock<ILogger<UserService>> _loggerMock;
        private readonly Mock<ILoggingService> _loggingServiceMock;
        private readonly Mock<IKeyStorageService> _keyStorageServiceMock;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ChatDbContext(options);
            _loggerMock = new Mock<ILogger<UserService>>();
            _loggingServiceMock = new Mock<ILoggingService>();
            _keyStorageServiceMock = new Mock<IKeyStorageService>();
            
            _userService = new UserService(_context, _loggerMock.Object, _loggingServiceMock.Object, _keyStorageServiceMock.Object);
        }

        [Fact]
        public async Task LoginAsync_ShouldLoginSuccessfully_WithValidCredentials()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = HashPassword(password),
                IsOnline = false,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.LoginAsync(username, password);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(username, result.Username);
            Assert.True(result.IsOnline);
            Assert.True(result.LastSeen > DateTime.UtcNow.AddMinutes(-1));
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogUserAction(user.Id.ToString(), "login", "User logged in successfully"),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var username = "nonexistent";
            var password = "password123";

            // Act & Assert
            await Assert.ThrowsAsync<UserNotFoundException>(() => 
                _userService.LoginAsync(username, password));
            
            // Verify security logging
            _loggingServiceMock.Verify(
                x => x.LogSecurityEvent("failed_login", username, "User not found", "WARNING"),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowInvalidCredentialsException_WithWrongPassword()
        {
            // Arrange
            var username = "testuser";
            var correctPassword = "password123";
            var wrongPassword = "wrongpassword";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = HashPassword(correctPassword),
                IsOnline = false,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => 
                _userService.LoginAsync(username, wrongPassword));
            
            // Verify security logging
            _loggingServiceMock.Verify(
                x => x.LogSecurityEvent("failed_login", username, "Invalid password", "WARNING"),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowArgumentException_WithEmptyUsername()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.LoginAsync("", "password"));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.LoginAsync(null, "password"));
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowArgumentException_WithEmptyPassword()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.LoginAsync("username", ""));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.LoginAsync("username", null));
        }

        [Fact]
        public async Task RegisterAsync_ShouldRegisterSuccessfully_WithValidData()
        {
            // Arrange
            var username = "newuser";
            var password = "password123";
            var email = "test@example.com";

            // Act
            var result = await _userService.RegisterAsync(username, password, email);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(username, result.Username);
            Assert.Equal(email, result.Email);
            Assert.False(result.IsOnline);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
            
            // Verify user was saved to database
            var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            Assert.NotNull(savedUser);
            Assert.Equal(username, savedUser.Username);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogUserAction(result.Id.ToString(), "register", "User registered successfully"),
                Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrowUsernameAlreadyExistsException_WhenUsernameExists()
        {
            // Arrange
            var username = "existinguser";
            var password = "password123";
            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = "hash",
                IsOnline = false,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<UsernameAlreadyExistsException>(() => 
                _userService.RegisterAsync(username, password));
            
            // Verify security logging
            _loggingServiceMock.Verify(
                x => x.LogSecurityEvent("registration_failed", username, "Username already exists", "WARNING"),
                Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrowArgumentException_WithEmptyUsername()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.RegisterAsync("", "password"));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.RegisterAsync(null, "password"));
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrowArgumentException_WithEmptyPassword()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.RegisterAsync("username", ""));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _userService.RegisterAsync("username", null));
        }

        [Fact]
        public async Task LogoutAsync_ShouldLogoutSuccessfully()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            await _userService.LogoutAsync(user.Id.ToString());

            // Assert
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.NotNull(updatedUser);
            Assert.False(updatedUser.IsOnline);
            Assert.True(updatedUser.LastSeen > DateTime.UtcNow.AddMinutes(-1));
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogUserAction(user.Id.ToString(), "logout", "User logged out successfully"),
                Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<UserNotFoundException>(() => 
                _userService.LogoutAsync(nonExistentUserId));
        }

        [Fact]
        public async Task GetOnlineUsersAsync_ShouldReturnOnlineUsers()
        {
            // Arrange
            var onlineUser1 = new User
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var onlineUser2 = new User
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var offlineUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "user3",
                PasswordHash = "hash",
                IsOnline = false,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.AddRange(onlineUser1, onlineUser2, offlineUser);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.GetOnlineUsersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, u => Assert.True(u.IsOnline));
            Assert.Contains(result, u => u.Username == "user1");
            Assert.Contains(result, u => u.Username == "user2");
        }

        [Fact]
        public async Task GetAllUsersAsync_ShouldReturnAllUsers()
        {
            // Arrange
            var user1 = new User
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var user2 = new User
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                PasswordHash = "hash",
                IsOnline = false,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.GetAllUsersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, u => u.Username == "user1");
            Assert.Contains(result, u => u.Username == "user2");
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldReturnUser_WhenUserExists()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.GetUserByIdAsync(user.Id.ToString());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.Id);
            Assert.Equal(user.Username, result.Username);
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<UserNotFoundException>(() => 
                _userService.GetUserByIdAsync(nonExistentUserId));
        }

        [Fact]
        public async Task UpdateUserStatusAsync_ShouldUpdateStatusSuccessfully()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = false,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            await _userService.UpdateUserStatusAsync(user.Id.ToString(), true);

            // Assert
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.NotNull(updatedUser);
            Assert.True(updatedUser.IsOnline);
            Assert.True(updatedUser.LastSeen > DateTime.UtcNow.AddMinutes(-1));
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogUserAction(user.Id.ToString(), "status_update", "User status updated to online"),
                Times.Once);
        }

        [Fact]
        public async Task ValidateUserAsync_ShouldReturnTrue_WhenUserExists()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.ValidateUserAsync(user.Id.ToString());

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateUserAsync_ShouldReturnFalse_WhenUserDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid().ToString();

            // Act
            var result = await _userService.ValidateUserAsync(nonExistentUserId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsUserOnlineAsync_ShouldReturnTrue_WhenUserIsOnline()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.IsUserOnlineAsync(user.Id.ToString());

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsUserOnlineAsync_ShouldReturnFalse_WhenUserIsOffline()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = false,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.IsUserOnlineAsync(user.Id.ToString());

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateLastSeenAsync_ShouldUpdateLastSeenSuccessfully()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PasswordHash = "hash",
                IsOnline = true,
                LastSeen = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var originalLastSeen = user.LastSeen;

            // Act
            await _userService.UpdateLastSeenAsync(user.Id.ToString());

            // Assert
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.NotNull(updatedUser);
            Assert.True(updatedUser.LastSeen > originalLastSeen);
        }

        private string HashPassword(string password)
        {
            // Simple hash for testing (same as in UserService)
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "salt"));
            return Convert.ToBase64String(hashedBytes);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
