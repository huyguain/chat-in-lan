using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;

namespace SecureLanChat.Services
{
    public class MessageService : IMessageService
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<MessageService> _logger;
        private readonly ILoggingService _loggingService;

        public MessageService(
            ChatDbContext context,
            ILogger<MessageService> logger,
            ILoggingService loggingService)
        {
            _context = context;
            _logger = logger;
            _loggingService = loggingService;
        }

        public async Task<Message> SaveMessageAsync(Message message)
        {
            try
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                _logger.LogDebug("Saving message from {SenderId} to {ReceiverId}", message.SenderId, message.ReceiverId);

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                _loggingService.LogMessageSent(
                    message.SenderId.ToString(),
                    message.ReceiverId?.ToString() ?? "broadcast",
                    message.ReceiverId == null,
                    message.MessageType.ToString());

                _logger.LogDebug("Message {MessageId} saved successfully", message.Id);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save message");
                throw new DatabaseException("Failed to save message", ex);
            }
        }

        public async Task SendMessageAsync(string senderId, string receiverId, string content)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId))
                    throw new ArgumentException("Sender ID cannot be null or empty", nameof(senderId));
                if (string.IsNullOrEmpty(receiverId))
                    throw new ArgumentException("Receiver ID cannot be null or empty", nameof(receiverId));
                if (string.IsNullOrEmpty(content))
                    throw new ArgumentException("Content cannot be null or empty", nameof(content));

                var now = DateTime.UtcNow;
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.Parse(senderId),
                    ReceiverId = Guid.Parse(receiverId),
                    Content = content,
                    MessageType = MessageType.Private,
                    CreatedAt = now,
                    Timestamp = now,
                    IV = string.Empty // Will be set during encryption
                };

                await SaveMessageAsync(message);
                _logger.LogInformation("Message sent from {SenderId} to {ReceiverId}", senderId, receiverId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message from {SenderId} to {ReceiverId}", senderId, receiverId);
                throw new DatabaseException("Failed to send message", ex);
            }
        }

        public async Task SendBroadcastAsync(string senderId, string content)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId))
                    throw new ArgumentException("Sender ID cannot be null or empty", nameof(senderId));
                if (string.IsNullOrEmpty(content))
                    throw new ArgumentException("Content cannot be null or empty", nameof(content));

                var now = DateTime.UtcNow;
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.Parse(senderId),
                    ReceiverId = null,
                    Content = content,
                    MessageType = MessageType.Broadcast,
                    CreatedAt = now,
                    Timestamp = now,
                    IV = string.Empty // Will be set during encryption
                };

                await SaveMessageAsync(message);
                _logger.LogInformation("Broadcast message sent from {SenderId}", senderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send broadcast from {SenderId}", senderId);
                throw new DatabaseException("Failed to send broadcast", ex);
            }
        }

        public async Task<List<Message>> GetMessageHistoryAsync(string userId, int page = 1, int pageSize = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Getting message history for user {UserId}, page {Page}, pageSize {PageSize}", 
                    userId, page, pageSize);

                var skip = (page - 1) * pageSize;

                var messages = await _context.Messages
                    .Where(m => m.SenderId.ToString() == userId || m.ReceiverId.ToString() == userId || m.ReceiverId == null)
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} messages for user {UserId}", messages.Count, userId);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message history for user {UserId}", userId);
                throw new DatabaseException("Failed to get message history", ex);
            }
        }

        public async Task<List<Message>> GetMessageHistoryAsync(string userId, string? otherUserId = null, int limit = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Getting message history for user {UserId}, other user {OtherUserId}, limit {Limit}", 
                    userId, otherUserId, limit);

                var query = _context.Messages.AsQueryable();

                if (string.IsNullOrEmpty(otherUserId))
                {
                    // Get all messages (broadcast messages)
                    query = query.Where(m => m.ReceiverId == null);
                }
                else
                {
                    // Get private messages between two users
                    query = query.Where(m => 
                        (m.SenderId.ToString() == userId && m.ReceiverId.ToString() == otherUserId) ||
                        (m.SenderId.ToString() == otherUserId && m.ReceiverId.ToString() == userId));
                }

                var messages = await query
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} messages for user {UserId}", messages.Count, userId);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message history for user {UserId}", userId);
                throw new DatabaseException("Failed to get message history", ex);
            }
        }

        public async Task<List<Message>> GetRecentMessagesAsync(int limit = 20)
        {
            try
            {
                _logger.LogDebug("Getting recent messages, limit {Limit}", limit);

                var messages = await _context.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} recent messages", messages.Count);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent messages");
                throw new DatabaseException("Failed to get recent messages", ex);
            }
        }

        public async Task<Message?> GetMessageByIdAsync(string messageId)
        {
            try
            {
                if (string.IsNullOrEmpty(messageId))
                    throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

                _logger.LogDebug("Getting message by ID {MessageId}", messageId);

                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id.ToString() == messageId);

                if (message == null)
                {
                    _logger.LogWarning("Message {MessageId} not found", messageId);
                    return null;
                }

                _logger.LogDebug("Message {MessageId} retrieved successfully", messageId);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message by ID {MessageId}", messageId);
                throw new DatabaseException("Failed to get message by ID", ex);
            }
        }

        public async Task<List<Message>> GetMessagesByUserAsync(string userId, int limit = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Getting messages by user {UserId}, limit {Limit}", userId, limit);

                var messages = await _context.Messages
                    .Where(m => m.SenderId.ToString() == userId)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} messages for user {UserId}", messages.Count, userId);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get messages by user {UserId}", userId);
                throw new DatabaseException("Failed to get messages by user", ex);
            }
        }

        public async Task<List<Message>> GetMessagesByTypeAsync(MessageType messageType, int limit = 50)
        {
            try
            {
                _logger.LogDebug("Getting messages by type {MessageType}, limit {Limit}", messageType, limit);

                var messages = await _context.Messages
                    .Where(m => m.MessageType == messageType)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} messages of type {MessageType}", messages.Count, messageType);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get messages by type {MessageType}", messageType);
                throw new DatabaseException("Failed to get messages by type", ex);
            }
        }

        public async Task DeleteMessageAsync(string messageId)
        {
            try
            {
                if (string.IsNullOrEmpty(messageId))
                    throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

                _logger.LogDebug("Deleting message {MessageId}", messageId);

                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id.ToString() == messageId);

                if (message == null)
                {
                    _logger.LogWarning("Message {MessageId} not found", messageId);
                    return;
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Message {MessageId} deleted successfully", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message {MessageId}", messageId);
                throw new DatabaseException("Failed to delete message", ex);
            }
        }

        public async Task<List<Message>> SearchMessagesAsync(string userId, string searchTerm)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                if (string.IsNullOrEmpty(searchTerm))
                    throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

                _logger.LogDebug("Searching messages with term '{SearchTerm}' for user {UserId}", 
                    searchTerm, userId);

                // Note: In a real implementation, you would need to decrypt messages for searching
                // For now, we'll search in the encrypted content (not ideal for production)
                var messages = await _context.Messages
                    .Where(m => (m.SenderId.ToString() == userId || m.ReceiverId.ToString() == userId || m.ReceiverId == null) &&
                               m.Content.Contains(searchTerm))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} messages matching search term '{SearchTerm}'", messages.Count, searchTerm);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search messages with term '{SearchTerm}'", searchTerm);
                throw new DatabaseException("Failed to search messages", ex);
            }
        }

        public async Task<List<Message>> SearchMessagesAsync(string searchTerm, string? userId = null, int limit = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                    throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

                _logger.LogDebug("Searching messages with term '{SearchTerm}', user {UserId}, limit {Limit}", 
                    searchTerm, userId, limit);

                var query = _context.Messages.AsQueryable();

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(m => m.SenderId.ToString() == userId);
                }

                // Note: In a real implementation, you would need to decrypt messages for searching
                // For now, we'll search in the encrypted content (not ideal for production)
                var messages = await query
                    .Where(m => m.Content.Contains(searchTerm))
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} messages matching search term '{SearchTerm}'", messages.Count, searchTerm);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search messages with term '{SearchTerm}'", searchTerm);
                throw new DatabaseException("Failed to search messages", ex);
            }
        }

        public async Task<int> GetMessageCountAsync(string? userId = null)
        {
            try
            {
                _logger.LogDebug("Getting message count for user {UserId}", userId);

                var query = _context.Messages.AsQueryable();

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(m => m.SenderId.ToString() == userId);
                }

                var count = await query.CountAsync();

                _logger.LogDebug("Message count: {Count} for user {UserId}", count, userId);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message count for user {UserId}", userId);
                throw new DatabaseException("Failed to get message count", ex);
            }
        }

        public async Task<List<Message>> GetMessagesByDateRangeAsync(DateTime startDate, DateTime endDate, string? userId = null, int limit = 100)
        {
            try
            {
                _logger.LogDebug("Getting messages by date range {StartDate} to {EndDate}, user {UserId}, limit {Limit}", 
                    startDate, endDate, userId, limit);

                var query = _context.Messages
                    .Where(m => m.CreatedAt >= startDate && m.CreatedAt <= endDate);

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(m => m.SenderId.ToString() == userId);
                }

                var messages = await query
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} messages in date range", messages.Count);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get messages by date range");
                throw new DatabaseException("Failed to get messages by date range", ex);
            }
        }

        public async Task CleanupOldMessagesAsync(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                _logger.LogInformation("Cleaning up messages older than {CutoffDate}", cutoffDate);

                var oldMessages = await _context.Messages
                    .Where(m => m.CreatedAt < cutoffDate)
                    .ToListAsync();

                if (oldMessages.Any())
                {
                    _context.Messages.RemoveRange(oldMessages);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Cleaned up {Count} old messages", oldMessages.Count);
                }
                else
                {
                    _logger.LogDebug("No old messages found for cleanup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old messages");
                throw new DatabaseException("Failed to cleanup old messages", ex);
            }
        }
    }
}