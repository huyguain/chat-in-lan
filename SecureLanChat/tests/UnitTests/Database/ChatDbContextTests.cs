using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Models;
using Xunit;

namespace SecureLanChat.Tests.Database
{
    public class ChatDbContextTests : IDisposable
    {
        private readonly ChatDbContext _context;

        public ChatDbContextTests()
        {
            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ChatDbContext(options);
        }

        [Fact]
        public async Task CanCreateUser()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "test-public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Assert
            var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            Assert.NotNull(savedUser);
            Assert.Equal("testuser", savedUser.Username);
            Assert.True(savedUser.IsOnline);
        }

        [Fact]
        public async Task CanCreateMessage()
        {
            // Arrange
            var sender = new User
            {
                Id = Guid.NewGuid(),
                Username = "sender",
                PublicKey = "sender-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var receiver = new User
            {
                Id = Guid.NewGuid(),
                Username = "receiver",
                PublicKey = "receiver-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                Content = "Test message",
                IV = "test-iv",
                MessageType = MessageType.Private,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            _context.Users.AddRange(sender, receiver);
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Assert
            var savedMessage = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .FirstOrDefaultAsync(m => m.Id == message.Id);

            Assert.NotNull(savedMessage);
            Assert.Equal("Test message", savedMessage.Content);
            Assert.Equal(MessageType.Private, savedMessage.MessageType);
            Assert.Equal("sender", savedMessage.Sender.Username);
            Assert.Equal("receiver", savedMessage.Receiver.Username);
        }

        [Fact]
        public async Task CanCreateSession()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "test-public-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ConnectionId = "test-connection-id",
                AESKey = "test-aes-key",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IsActive = true
            };

            // Act
            _context.Users.Add(user);
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            // Assert
            var savedSession = await _context.Sessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == session.Id);

            Assert.NotNull(savedSession);
            Assert.Equal("test-connection-id", savedSession.ConnectionId);
            Assert.True(savedSession.IsActive);
            Assert.Equal("testuser", savedSession.User.Username);
        }

        [Fact]
        public async Task UsernameMustBeUnique()
        {
            // Arrange
            var user1 = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                PublicKey = "key1",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var user2 = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser", // Same username
                PublicKey = "key2",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            _context.Users.Add(user1);
            await _context.SaveChangesAsync();

            _context.Users.Add(user2);

            // Assert
            await Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
        }

        [Fact]
        public async Task CanQueryMessagesBySender()
        {
            // Arrange
            var sender = new User
            {
                Id = Guid.NewGuid(),
                Username = "sender",
                PublicKey = "sender-key",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var messages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = sender.Id,
                    Content = "Message 1",
                    IV = "iv1",
                    MessageType = MessageType.Broadcast,
                    CreatedAt = DateTime.UtcNow
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = sender.Id,
                    Content = "Message 2",
                    IV = "iv2",
                    MessageType = MessageType.Broadcast,
                    CreatedAt = DateTime.UtcNow
                }
            };

            // Act
            _context.Users.Add(sender);
            _context.Messages.AddRange(messages);
            await _context.SaveChangesAsync();

            var senderMessages = await _context.Messages
                .Where(m => m.SenderId == sender.Id)
                .ToListAsync();

            // Assert
            Assert.Equal(2, senderMessages.Count);
        }

        [Fact]
        public async Task CanQueryOnlineUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "user1",
                    PublicKey = "key1",
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "user2",
                    PublicKey = "key2",
                    IsOnline = false,
                    LastSeen = DateTime.UtcNow.AddHours(-1),
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "user3",
                    PublicKey = "key3",
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }
            };

            // Act
            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();

            var onlineUsers = await _context.Users
                .Where(u => u.IsOnline)
                .ToListAsync();

            // Assert
            Assert.Equal(2, onlineUsers.Count);
            Assert.All(onlineUsers, u => Assert.True(u.IsOnline));
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
