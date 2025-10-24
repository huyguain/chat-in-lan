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
    public class MessageServiceTests : IDisposable
    {
        private readonly ChatDbContext _context;
        private readonly Mock<ILogger<MessageService>> _loggerMock;
        private readonly Mock<ILoggingService> _loggingServiceMock;
        private readonly MessageService _messageService;

        public MessageServiceTests()
        {
            var options = new DbContextOptionsBuilder<ChatDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ChatDbContext(options);
            _loggerMock = new Mock<ILogger<MessageService>>();
            _loggingServiceMock = new Mock<ILoggingService>();
            
            _messageService = new MessageService(_context, _loggerMock.Object, _loggingServiceMock.Object);
        }

        [Fact]
        public async Task SaveMessageAsync_ShouldSaveMessageSuccessfully()
        {
            // Arrange
            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.NewGuid(),
                ReceiverId = Guid.NewGuid(),
                Content = "Test message",
                IV = "test-iv",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            // Act
            var result = await _messageService.SaveMessageAsync(message);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(message.Id, result.Id);
            Assert.Equal(message.Content, result.Content);
            
            // Verify message was saved to database
            var savedMessage = await _context.Messages.FindAsync(message.Id);
            Assert.NotNull(savedMessage);
            Assert.Equal(message.Content, savedMessage.Content);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogMessageSent(message.SenderId.ToString(), message.ReceiverId.ToString(), false, "Text"),
                Times.Once);
        }

        [Fact]
        public async Task SaveMessageAsync_ShouldThrowArgumentNullException_WhenMessageIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _messageService.SaveMessageAsync(null));
        }

        [Fact]
        public async Task GetMessageHistoryAsync_ShouldReturnMessages_ForValidUser()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var otherUserId = Guid.NewGuid().ToString();
            
            var messages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.Parse(userId),
                    ReceiverId = Guid.Parse(otherUserId),
                    Content = "Message 1",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddMinutes(-10)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.Parse(otherUserId),
                    ReceiverId = Guid.Parse(userId),
                    Content = "Message 2",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddMinutes(-5)
                }
            };

            _context.Messages.AddRange(messages);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetMessageHistoryAsync(userId, otherUserId, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, m => Assert.True(
                m.SenderId.ToString() == userId || m.ReceiverId?.ToString() == userId));
        }

        [Fact]
        public async Task GetMessageHistoryAsync_ShouldReturnBroadcastMessages_WhenOtherUserIdIsNull()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            
            var broadcastMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.Parse(userId),
                ReceiverId = null, // Broadcast message
                Content = "Broadcast message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            var privateMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.Parse(userId),
                ReceiverId = Guid.NewGuid(),
                Content = "Private message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.AddRange(broadcastMessage, privateMessage);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetMessageHistoryAsync(userId, null, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(broadcastMessage.Id, result[0].Id);
        }

        [Fact]
        public async Task GetMessageHistoryAsync_ShouldThrowArgumentException_WhenUserIdIsEmpty()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.GetMessageHistoryAsync("", null, 10));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.GetMessageHistoryAsync(null, null, 10));
        }

        [Fact]
        public async Task GetRecentMessagesAsync_ShouldReturnRecentMessages()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Old message",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddHours(-2)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Recent message",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                }
            };

            _context.Messages.AddRange(messages);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetRecentMessagesAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Recent message", result[0].Content);
        }

        [Fact]
        public async Task GetMessageByIdAsync_ShouldReturnMessage_WhenMessageExists()
        {
            // Arrange
            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.NewGuid(),
                Content = "Test message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetMessageByIdAsync(message.Id.ToString());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(message.Id, result.Id);
            Assert.Equal(message.Content, result.Content);
        }

        [Fact]
        public async Task GetMessageByIdAsync_ShouldReturnNull_WhenMessageDoesNotExist()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid().ToString();

            // Act
            var result = await _messageService.GetMessageByIdAsync(nonExistentId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetMessageByIdAsync_ShouldThrowArgumentException_WhenMessageIdIsEmpty()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.GetMessageByIdAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.GetMessageByIdAsync(null));
        }

        [Fact]
        public async Task GetMessagesByUserAsync_ShouldReturnUserMessages()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            
            var userMessages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = userId,
                    Content = "User message 1",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddMinutes(-10)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = userId,
                    Content = "User message 2",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddMinutes(-5)
                }
            };

            var otherUserMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = otherUserId,
                Content = "Other user message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.AddRange(userMessages);
            _context.Messages.Add(otherUserMessage);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetMessagesByUserAsync(userId.ToString(), 10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, m => Assert.Equal(userId, m.SenderId));
        }

        [Fact]
        public async Task GetMessagesByTypeAsync_ShouldReturnMessagesByType()
        {
            // Arrange
            var textMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.NewGuid(),
                Content = "Text message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            var imageMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.NewGuid(),
                Content = "Image message",
                MessageType = MessageType.Image,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.AddRange(textMessage, imageMessage);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetMessagesByTypeAsync(MessageType.Text, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(MessageType.Text, result[0].MessageType);
        }

        [Fact]
        public async Task DeleteMessageAsync_ShouldDeleteMessage_WhenUserIsAuthorized()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                Content = "Message to delete",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.DeleteMessageAsync(message.Id.ToString(), userId.ToString());

            // Assert
            Assert.True(result);
            
            var deletedMessage = await _context.Messages.FindAsync(message.Id);
            Assert.Null(deletedMessage);
        }

        [Fact]
        public async Task DeleteMessageAsync_ShouldReturnFalse_WhenUserIsNotAuthorized()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                Content = "Message to delete",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.DeleteMessageAsync(message.Id.ToString(), otherUserId.ToString());

            // Assert
            Assert.False(result);
            
            var messageStillExists = await _context.Messages.FindAsync(message.Id);
            Assert.NotNull(messageStillExists);
        }

        [Fact]
        public async Task DeleteMessageAsync_ShouldThrowArgumentException_WhenMessageIdIsEmpty()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.DeleteMessageAsync("", "user-id"));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.DeleteMessageAsync("message-id", ""));
        }

        [Fact]
        public async Task SearchMessagesAsync_ShouldReturnMatchingMessages()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Hello world",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Goodbye world",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                }
            };

            _context.Messages.AddRange(messages);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.SearchMessagesAsync("world", null, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, m => Assert.Contains("world", m.Content));
        }

        [Fact]
        public async Task SearchMessagesAsync_ShouldThrowArgumentException_WhenSearchTermIsEmpty()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.SearchMessagesAsync("", null, 10));
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _messageService.SearchMessagesAsync(null, null, 10));
        }

        [Fact]
        public async Task GetMessageCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var messages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = userId,
                    Content = "Message 1",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = userId,
                    Content = "Message 2",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Other message",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow
                }
            };

            _context.Messages.AddRange(messages);
            await _context.SaveChangesAsync();

            // Act
            var totalCount = await _messageService.GetMessageCountAsync();
            var userCount = await _messageService.GetMessageCountAsync(userId.ToString());

            // Assert
            Assert.Equal(3, totalCount);
            Assert.Equal(2, userCount);
        }

        [Fact]
        public async Task GetMessagesByDateRangeAsync_ShouldReturnMessagesInRange()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-1);
            var endDate = DateTime.UtcNow;
            
            var messages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Old message",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddDays(-2)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    Content = "Recent message",
                    MessageType = MessageType.Text,
                    Timestamp = DateTime.UtcNow.AddHours(-1)
                }
            };

            _context.Messages.AddRange(messages);
            await _context.SaveChangesAsync();

            // Act
            var result = await _messageService.GetMessagesByDateRangeAsync(startDate, endDate, null, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Recent message", result[0].Content);
        }

        [Fact]
        public async Task CleanupOldMessagesAsync_ShouldRemoveOldMessages()
        {
            // Arrange
            var oldMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.NewGuid(),
                Content = "Old message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow.AddDays(-35)
            };

            var recentMessage = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = Guid.NewGuid(),
                Content = "Recent message",
                MessageType = MessageType.Text,
                Timestamp = DateTime.UtcNow.AddDays(-5)
            };

            _context.Messages.AddRange(oldMessage, recentMessage);
            await _context.SaveChangesAsync();

            // Act
            await _messageService.CleanupOldMessagesAsync(30);

            // Assert
            var remainingMessages = await _context.Messages.ToListAsync();
            Assert.Single(remainingMessages);
            Assert.Equal(recentMessage.Id, remainingMessages[0].Id);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
