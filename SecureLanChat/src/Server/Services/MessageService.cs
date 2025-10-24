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
                    .OrderByDescending(m => m.Timestamp)
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
                    .OrderByDescending(m => m.Timestamp)
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

        public async Task<bool> DeleteMessageAsync(string messageId, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(messageId))
                    throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));
                
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Deleting message {MessageId} by user {UserId}", messageId, userId);

                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id.ToString() == messageId && m.SenderId.ToString() == userId);

                if (message == null)
                {
                    _logger.LogWarning("Message {MessageId} not found or user {UserId} not authorized", messageId, userId);
                    return false;
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Message {MessageId} deleted successfully", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message {MessageId}", messageId);
                throw new DatabaseException("Failed to delete message", ex);
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
                    .OrderByDescending(m => m.Timestamp)
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
                    .Where(m => m.Timestamp >= startDate && m.Timestamp <= endDate);

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
                    .Where(m => m.Timestamp < cutoffDate)
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