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
    public class KeyStorageServiceTests : IDisposable
    {
        private readonly ChatDbContext _context;
        private readonly Mock<ILogger<KeyStorageService>> _loggerMock;
        private readonly Mock<ILoggingService> _loggingServiceMock;
        private readonly KeyStorageService _keyStorageService;

        public KeyStorageServiceTests()
        {
            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ChatDbContext(options);
            _loggerMock = new Mock<ILogger<KeyStorageService>>();
            _loggingServiceMock = new Mock<ILoggingService>();
            
            _keyStorageService = new KeyStorageService(_context, _loggerMock.Object, _loggingServiceMock.Object);
        }

        [Fact]
        public async Task StoreUserPublicKeyAsync_ShouldStoreKeySuccessfully()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "old-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var newPublicKey = "new-public-key";

            // Act
            var result = await _keyStorageService.StoreUserPublicKeyAsync(user.Id.ToString(), newPublicKey);

            // Assert
            Assert.Equal(newPublicKey, result);
            
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.Equal(newPublicKey, updatedUser.PublicKey);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogSecurityEvent("key_storage", user.Id.ToString(), "Public key stored successfully", "INFO"),
                Times.Once);
        }

        [Fact]
        public async Task StoreUserPublicKeyAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid().ToString();
            var publicKey = "test-public-key";

            // Act & Assert
            await Assert.ThrowsAsync<UserNotFoundException>(() => 
                _keyStorageService.StoreUserPublicKeyAsync(nonExistentUserId, publicKey));
        }

        [Fact]
        public async Task StoreUserPublicKeyAsync_ShouldThrowArgumentException_WhenUserIdIsNull()
        {
            // Arrange
            var publicKey = "test-public-key";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _keyStorageService.StoreUserPublicKeyAsync(null, publicKey));
        }

        [Fact]
        public async Task StoreUserPublicKeyAsync_ShouldThrowArgumentException_WhenPublicKeyIsNull()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "old-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _keyStorageService.StoreUserPublicKeyAsync(user.Id.ToString(), null));
        }

        [Fact]
        public async Task GetUserPublicKeyAsync_ShouldReturnPublicKey_WhenUserExists()
        {
            // Arrange
            var publicKey = "test-public-key";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = publicKey,
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _keyStorageService.GetUserPublicKeyAsync(user.Id.ToString());

            // Assert
            Assert.Equal(publicKey, result);
        }

        [Fact]
        public async Task GetUserPublicKeyAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<UserNotFoundException>(() => 
                _keyStorageService.GetUserPublicKeyAsync(nonExistentUserId));
        }

        [Fact]
        public async Task GetUserPublicKeyAsync_ShouldThrowEncryptionException_WhenUserHasNoPublicKey()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = null, // No public key
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                _keyStorageService.GetUserPublicKeyAsync(user.Id.ToString()));
        }

        [Fact]
        public async Task StoreSessionKeyAsync_ShouldStoreNewSessionKey()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var connectionId = "connection-123";
            var aesKey = "aes-key-123";

            // Act
            var result = await _keyStorageService.StoreSessionKeyAsync(user.Id.ToString(), connectionId, aesKey);

            // Assert
            Assert.Equal(aesKey, result);
            
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.ConnectionId == connectionId);
            
            Assert.NotNull(session);
            Assert.Equal(aesKey, session.AESKey);
            Assert.True(session.IsActive);
            Assert.True(session.ExpiresAt > DateTime.UtcNow);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogSecurityEvent("session_key_storage", user.Id.ToString(), "Session key stored successfully", "INFO"),
                Times.Once);
        }

        [Fact]
        public async Task StoreSessionKeyAsync_ShouldUpdateExistingSessionKey()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var connectionId = "connection-123";
            var oldAesKey = "old-aes-key";
            var newAesKey = "new-aes-key";

            // Create existing session
            var existingSession = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConnectionId = connectionId,
                AESKey = oldAesKey,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsActive = true
            };

            _context.Sessions.Add(existingSession);
            await _context.SaveChangesAsync();

            // Act
            var result = await _keyStorageService.StoreSessionKeyAsync(user.Id.ToString(), connectionId, newAesKey);

            // Assert
            Assert.Equal(newAesKey, result);
            
            var updatedSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.ConnectionId == connectionId);
            
            Assert.NotNull(updatedSession);
            Assert.Equal(newAesKey, updatedSession.AESKey);
            Assert.True(updatedSession.IsActive);
            Assert.True(updatedSession.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task GetSessionKeyAsync_ShouldReturnSessionKey_WhenSessionExists()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var connectionId = "connection-123";
            var aesKey = "aes-key-123";

            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConnectionId = connectionId,
                AESKey = aesKey,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsActive = true
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            // Act
            var result = await _keyStorageService.GetSessionKeyAsync(user.Id.ToString(), connectionId);

            // Assert
            Assert.Equal(aesKey, result);
        }

        [Fact]
        public async Task GetSessionKeyAsync_ShouldThrowSessionExpiredException_WhenSessionIsExpired()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var connectionId = "connection-123";
            var aesKey = "aes-key-123";

            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConnectionId = connectionId,
                AESKey = aesKey,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
                IsActive = true
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<SessionExpiredException>(() => 
                _keyStorageService.GetSessionKeyAsync(user.Id.ToString(), connectionId));
        }

        [Fact]
        public async Task ValidateStoredKeyAsync_ShouldReturnTrue_ForValidPublicKey()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "valid-public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var isValid = await _keyStorageService.ValidateStoredKeyAsync(user.Id.ToString(), "public");

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateStoredKeyAsync_ShouldReturnFalse_ForInvalidKeyType()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "valid-public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var isValid = await _keyStorageService.ValidateStoredKeyAsync(user.Id.ToString(), "invalid");

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task CleanupExpiredKeysAsync_ShouldRemoveExpiredSessions()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var expiredSession = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConnectionId = "expired-connection",
                AESKey = "expired-key",
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
                IsActive = true
            };

            var activeSession = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConnectionId = "active-connection",
                AESKey = "active-key",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // Not expired
                IsActive = true
            };

            _context.Sessions.AddRange(expiredSession, activeSession);
            await _context.SaveChangesAsync();

            // Act
            await _keyStorageService.CleanupExpiredKeysAsync();

            // Assert
            var remainingSessions = await _context.Sessions.CountAsync();
            Assert.Equal(1, remainingSessions);
            
            var remainingSession = await _context.Sessions.FirstAsync();
            Assert.Equal("active-connection", remainingSession.ConnectionId);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogSecurityEvent("key_cleanup", "system", "Cleaned up 1 expired sessions", "INFO"),
                Times.Once);
        }

        [Fact]
        public async Task KeyExistsAsync_ShouldReturnTrue_WhenKeyExists()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var exists = await _keyStorageService.KeyExistsAsync(user.Id.ToString(), "public");

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task KeyExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid().ToString();

            // Act
            var exists = await _keyStorageService.KeyExistsAsync(nonExistentUserId, "public");

            // Assert
            Assert.False(exists);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
