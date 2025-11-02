using Microsoft.AspNetCore.SignalR;
using SecureLanChat.Hubs;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;

namespace SecureLanChat.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;
        private readonly ILoggingService _loggingService;

        public NotificationService(
            IHubContext<ChatHub> hubContext,
            ILogger<NotificationService> logger,
            ILoggingService loggingService)
        {
            _hubContext = hubContext;
            _logger = logger;
            _loggingService = loggingService;
        }

        public async Task NotifyUserOnlineAsync(string userId, string username)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Notifying user online: {UserId}", userId);

                await _hubContext.Clients.All.SendAsync("UserOnline", new
                {
                    Id = userId,
                    Username = username,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow
                });

                _loggingService.LogUserConnection(userId, "system", true);
                _logger.LogDebug("User online notification sent for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify user online for {UserId}", userId);
                throw new NotificationException("Failed to notify user online", ex);
            }
        }

        public async Task NotifyUserOfflineAsync(string userId, string username)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Notifying user offline: {UserId}", userId);

                await _hubContext.Clients.All.SendAsync("UserOffline", new
                {
                    Id = userId,
                    Username = username,
                    IsOnline = false,
                    LastSeen = DateTime.UtcNow
                });

                _loggingService.LogUserConnection(userId, "system", false);
                _logger.LogDebug("User offline notification sent for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify user offline for {UserId}", userId);
                throw new NotificationException("Failed to notify user offline", ex);
            }
        }

        public async Task NotifyNewMessageAsync(string senderId, string senderUsername, string? receiverId, string messageType)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId))
                    throw new ArgumentException("Sender ID cannot be null or empty", nameof(senderId));

                _logger.LogDebug("Notifying new message from {SenderId} to {ReceiverId}", senderId, receiverId);

                var notification = new
                {
                    SenderId = senderId,
                    SenderUsername = senderUsername,
                    ReceiverId = receiverId,
                    MessageType = messageType,
                    Timestamp = DateTime.UtcNow
                };

                if (string.IsNullOrEmpty(receiverId))
                {
                    // Broadcast notification to all users
                    await _hubContext.Clients.All.SendAsync("NewMessage", notification);
                    _logger.LogDebug("Broadcast message notification sent from {SenderId}", senderId);
                }
                else
                {
                    // Send notification to specific user
                    await _hubContext.Clients.Group($"user_{receiverId}").SendAsync("NewMessage", notification);
                    _logger.LogDebug("Private message notification sent from {SenderId} to {ReceiverId}", senderId, receiverId);
                }

                _loggingService.LogMessageReceived(receiverId ?? "broadcast", senderId, string.IsNullOrEmpty(receiverId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify new message from {SenderId}", senderId);
                throw new NotificationException("Failed to notify new message", ex);
            }
        }

        public async Task NotifySystemMessageAsync(string message, string messageType = "system")
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));

                _logger.LogDebug("Notifying system message: {Message}", message);

                var notification = new
                {
                    Message = message,
                    MessageType = messageType,
                    Timestamp = DateTime.UtcNow,
                    IsSystem = true
                };

                await _hubContext.Clients.All.SendAsync("SystemMessage", notification);
                _logger.LogDebug("System message notification sent: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify system message: {Message}", message);
                throw new NotificationException("Failed to notify system message", ex);
            }
        }

        public async Task NotifyUserTypingAsync(string userId, string username, string? receiverId, bool isTyping)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Notifying typing status for user {UserId}: {IsTyping}", userId, isTyping);

                var notification = new
                {
                    UserId = userId,
                    Username = username,
                    ReceiverId = receiverId,
                    IsTyping = isTyping,
                    Timestamp = DateTime.UtcNow
                };

                if (string.IsNullOrEmpty(receiverId))
                {
                    // Broadcast typing status to all users
                    await _hubContext.Clients.All.SendAsync("UserTyping", notification);
                }
                else
                {
                    // Send typing status to specific user
                    await _hubContext.Clients.Group($"user_{receiverId}").SendAsync("UserTyping", notification);
                }

                _logger.LogDebug("Typing notification sent for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify typing status for user {UserId}", userId);
                throw new NotificationException("Failed to notify typing status", ex);
            }
        }

        public async Task NotifyConnectionStatusAsync(string userId, string status, string? details = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Notifying connection status for user {UserId}: {Status}", userId, status);

                var notification = new
                {
                    UserId = userId,
                    Status = status,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"user_{userId}").SendAsync("ConnectionStatus", notification);
                _logger.LogDebug("Connection status notification sent for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify connection status for user {UserId}", userId);
                throw new NotificationException("Failed to notify connection status", ex);
            }
        }

        public async Task NotifyErrorAsync(string userId, string errorMessage, string? errorCode = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Notifying error for user {UserId}: {ErrorMessage}", userId, errorMessage);

                var notification = new
                {
                    UserId = userId,
                    ErrorMessage = errorMessage,
                    ErrorCode = errorCode,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"user_{userId}").SendAsync("Error", notification);
                _logger.LogDebug("Error notification sent for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify error for user {UserId}", userId);
                throw new NotificationException("Failed to notify error", ex);
            }
        }

        public async Task NotifySuccessAsync(string userId, string message, string? details = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Notifying success for user {UserId}: {Message}", userId, message);

                var notification = new
                {
                    UserId = userId,
                    Message = message,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"user_{userId}").SendAsync("Success", notification);
                _logger.LogDebug("Success notification sent for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify success for user {UserId}", userId);
                throw new NotificationException("Failed to notify success", ex);
            }
        }

        public async Task NotifyOnlineUsersUpdateAsync(List<User> onlineUsers)
        {
            try
            {
                _logger.LogDebug("Notifying online users update: {Count} users", onlineUsers.Count);

                var users = onlineUsers.Select(u => new
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    IsOnline = u.IsOnline,
                    LastSeen = u.LastSeen
                }).ToList();

                await _hubContext.Clients.All.SendAsync("OnlineUsers", users);
                _logger.LogDebug("Online users update notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify online users update");
                throw new NotificationException("Failed to notify online users update", ex);
            }
        }

        public async Task NotifyMessageDeletedAsync(string messageId, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(messageId))
                    throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

                _logger.LogDebug("Notifying message deleted: {MessageId} by user {UserId}", messageId, userId);

                var notification = new
                {
                    MessageId = messageId,
                    DeletedBy = userId,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("MessageDeleted", notification);
                _logger.LogDebug("Message deleted notification sent for {MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify message deleted for {MessageId}", messageId);
                throw new NotificationException("Failed to notify message deleted", ex);
            }
        }

        public async Task SendNotificationAsync(string userId, string message, NotificationType type)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));

                _logger.LogDebug("Sending notification to user {UserId}, type {Type}", userId, type);

                var notification = new
                {
                    UserId = userId,
                    Message = message,
                    Type = type.ToString(),
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"user_{userId}").SendAsync("Notification", notification);
                _logger.LogDebug("Notification sent to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
                throw new NotificationException("Failed to send notification", ex);
            }
        }

        public async Task SendDesktopNotificationAsync(string title, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(title))
                    throw new ArgumentException("Title cannot be null or empty", nameof(title));
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));

                _logger.LogDebug("Sending desktop notification: {Title}", title);

                var notification = new
                {
                    Title = title,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("DesktopNotification", notification);
                _logger.LogDebug("Desktop notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send desktop notification");
                throw new NotificationException("Failed to send desktop notification", ex);
            }
        }

        public async Task PlayNotificationSoundAsync()
        {
            try
            {
                _logger.LogDebug("Playing notification sound");

                await _hubContext.Clients.All.SendAsync("PlaySound", new { Type = "notification" });
                _logger.LogDebug("Notification sound played");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to play notification sound");
                throw new NotificationException("Failed to play notification sound", ex);
            }
        }

        // Unread count tracking - simple in-memory implementation
        private static readonly Dictionary<string, int> _unreadCounts = new Dictionary<string, int>();

        public async Task UpdateUnreadCountAsync(string userId, int count)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

                _logger.LogDebug("Updating unread count for user {UserId} to {Count}", userId, count);

                _unreadCounts[userId] = count;

                var notification = new
                {
                    UserId = userId,
                    UnreadCount = count,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"user_{userId}").SendAsync("UnreadCountUpdate", notification);
                _logger.LogDebug("Unread count updated for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update unread count for user {UserId}", userId);
                throw new NotificationException("Failed to update unread count", ex);
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return 0;

                _logger.LogDebug("Getting unread count for user {UserId}", userId);

                var count = _unreadCounts.TryGetValue(userId, out var unreadCount) ? unreadCount : 0;

                return await Task.FromResult(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get unread count for user {UserId}", userId);
                return 0;
            }
        }
    }
}