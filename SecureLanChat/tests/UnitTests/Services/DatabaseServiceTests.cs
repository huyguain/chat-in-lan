using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Models;
using SecureLanChat.Services;
using Xunit;

namespace SecureLanChat.Tests.Services
{
    public class DatabaseServiceTests : IDisposable
    {
        private readonly ChatDbContext _context;
        private readonly DatabaseService _databaseService;

        public DatabaseServiceTests()
        {
            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ChatDbContext(options);
            var logger = new LoggerFactory().CreateLogger<DatabaseService>();
            _databaseService = new DatabaseService(_context, logger);
        }

        [Fact]
        public async Task IsDatabaseConnectedAsync_ShouldReturnTrue_WhenDatabaseIsAccessible()
        {
            // Act
            var result = await _databaseService.IsDatabaseConnectedAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task TestConnectionAsync_ShouldReturnTrue_WhenDatabaseIsWorking()
        {
            // Act
            var result = await _databaseService.TestConnectionAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetConnectionCountAsync_ShouldReturnZero_WhenNoActiveSessions()
        {
            // Act
            var result = await _databaseService.GetConnectionCountAsync();

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetConnectionCountAsync_ShouldReturnCorrectCount_WhenActiveSessionsExist()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "test-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var sessions = new List<Session>
            {
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn1",
                    AESKey = "aes1",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    IsActive = true
                },
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn2",
                    AESKey = "aes2",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    IsActive = true
                },
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn3",
                    AESKey = "aes3",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    IsActive = false
                }
            };

            _context.Users.Add(user);
            _context.Sessions.AddRange(sessions);
            await _context.SaveChangesAsync();

            // Act
            var result = await _databaseService.GetConnectionCountAsync();

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task CleanupExpiredSessionsAsync_ShouldRemoveExpiredSessions()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "test-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var sessions = new List<Session>
            {
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn1",
                    AESKey = "aes1",
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
                    IsActive = true
                },
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn2",
                    AESKey = "aes2",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1), // Not expired
                    IsActive = true
                }
            };

            _context.Users.Add(user);
            _context.Sessions.AddRange(sessions);
            await _context.SaveChangesAsync();

            // Act
            await _databaseService.CleanupExpiredSessionsAsync();

            // Assert
            var remainingSessions = await _context.Sessions.CountAsync();
            Assert.Equal(1, remainingSessions);

            var activeSession = await _context.Sessions.FirstAsync();
            Assert.Equal("conn2", activeSession.ConnectionId);
        }

        [Fact]
        public async Task CleanupExpiredSessionsAsync_ShouldNotRemoveActiveSessions()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "test-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var sessions = new List<Session>
            {
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn1",
                    AESKey = "aes1",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    IsActive = true
                },
                new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ConnectionId = "conn2",
                    AESKey = "aes2",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(2),
                    IsActive = true
                }
            };

            _context.Users.Add(user);
            _context.Sessions.AddRange(sessions);
            await _context.SaveChangesAsync();

            // Act
            await _databaseService.CleanupExpiredSessionsAsync();

            // Assert
            var remainingSessions = await _context.Sessions.CountAsync();
            Assert.Equal(2, remainingSessions);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
