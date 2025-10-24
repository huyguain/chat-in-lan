using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Models;
using SecureLanChat.Services;
using Xunit;

namespace SecureLanChat.Tests.Services
{
    public class DatabaseSeedingServiceTests : IDisposable
    {
        private readonly ChatDbContext _context;
        private readonly DatabaseSeedingService _seedingService;

        public DatabaseSeedingServiceTests()
        {
            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ChatDbContext(options);
            var logger = new LoggerFactory().CreateLogger<DatabaseSeedingService>();
            _seedingService = new DatabaseSeedingService(_context, logger);
        }

        [Fact]
        public async Task SeedAsync_ShouldCreateTestUsers_WhenDatabaseIsEmpty()
        {
            // Act
            await _seedingService.SeedAsync();

            // Assert
            var users = await _context.Users.ToListAsync();
            Assert.True(users.Count >= 4); // At least 4 test users

            var usernames = users.Select(u => u.Username).ToList();
            Assert.Contains("admin", usernames);
            Assert.Contains("user1", usernames);
            Assert.Contains("user2", usernames);
            Assert.Contains("testuser", usernames);
        }

        [Fact]
        public async Task SeedAsync_ShouldNotCreateUsers_WhenDatabaseAlreadyHasData()
        {
            // Arrange
            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "existinguser",
                PublicKey = "existing-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            // Act
            await _seedingService.SeedAsync();

            // Assert
            var users = await _context.Users.ToListAsync();
            Assert.Single(users);
            Assert.Equal("existinguser", users.First().Username);
        }

        [Fact]
        public async Task SeedTestUsersAsync_ShouldCreateUsersWithValidData()
        {
            // Act
            await _seedingService.SeedTestUsersAsync();

            // Assert
            var users = await _context.Users.ToListAsync();
            Assert.Equal(4, users.Count);

            foreach (var user in users)
            {
                Assert.NotEqual(Guid.Empty, user.Id);
                Assert.NotEmpty(user.Username);
                Assert.NotEmpty(user.PublicKey);
                Assert.True(user.CreatedAt <= DateTime.UtcNow);
            }
        }

        [Fact]
        public async Task SeedTestMessagesAsync_ShouldCreateMessages_WhenUsersExist()
        {
            // Arrange
            await _seedingService.SeedTestUsersAsync();

            // Act
            await _seedingService.SeedTestMessagesAsync();

            // Assert
            var messages = await _context.Messages.ToListAsync();
            Assert.True(messages.Count >= 4);

            var broadcastMessages = messages.Where(m => m.MessageType == MessageType.Broadcast).ToList();
            var privateMessages = messages.Where(m => m.MessageType == MessageType.Private).ToList();

            Assert.True(broadcastMessages.Count >= 2);
            Assert.True(privateMessages.Count >= 2);

            // Check that all messages have valid senders
            foreach (var message in messages)
            {
                Assert.NotEqual(Guid.Empty, message.SenderId);
                Assert.NotEmpty(message.Content);
                Assert.NotEmpty(message.IV);
            }
        }

        [Fact]
        public async Task SeedTestMessagesAsync_ShouldNotCreateMessages_WhenNoUsersExist()
        {
            // Act
            await _seedingService.SeedTestMessagesAsync();

            // Assert
            var messages = await _context.Messages.ToListAsync();
            Assert.Empty(messages);
        }

        [Fact]
        public async Task ClearAllDataAsync_ShouldRemoveAllData()
        {
            // Arrange
            await _seedingService.SeedAsync();

            // Verify data exists
            var userCount = await _context.Users.CountAsync();
            var messageCount = await _context.Messages.CountAsync();
            Assert.True(userCount > 0);
            Assert.True(messageCount > 0);

            // Act
            await _seedingService.ClearAllDataAsync();

            // Assert
            Assert.Equal(0, await _context.Users.CountAsync());
            Assert.Equal(0, await _context.Messages.CountAsync());
            Assert.Equal(0, await _context.Sessions.CountAsync());
        }

        [Fact]
        public async Task SeedTestUsersAsync_ShouldCreateUsersWithDifferentStatuses()
        {
            // Act
            await _seedingService.SeedTestUsersAsync();

            // Assert
            var users = await _context.Users.ToListAsync();
            
            var onlineUsers = users.Where(u => u.IsOnline).ToList();
            var offlineUsers = users.Where(u => !u.IsOnline).ToList();

            Assert.True(onlineUsers.Count > 0);
            Assert.True(offlineUsers.Count > 0);

            // Check that online users have recent LastSeen
            foreach (var user in onlineUsers)
            {
                Assert.True(user.LastSeen > DateTime.UtcNow.AddHours(-1));
            }
        }

        [Fact]
        public async Task SeedTestMessagesAsync_ShouldCreateMessagesWithDifferentTypes()
        {
            // Arrange
            await _seedingService.SeedTestUsersAsync();

            // Act
            await _seedingService.SeedTestMessagesAsync();

            // Assert
            var messages = await _context.Messages.ToListAsync();
            
            var broadcastMessages = messages.Where(m => m.MessageType == MessageType.Broadcast).ToList();
            var privateMessages = messages.Where(m => m.MessageType == MessageType.Private).ToList();

            Assert.True(broadcastMessages.Count > 0);
            Assert.True(privateMessages.Count > 0);

            // Check broadcast messages have no receiver
            foreach (var message in broadcastMessages)
            {
                Assert.Null(message.ReceiverId);
            }

            // Check private messages have receiver
            foreach (var message in privateMessages)
            {
                Assert.NotNull(message.ReceiverId);
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
