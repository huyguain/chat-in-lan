using Microsoft.AspNetCore.SignalR;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.Security.Claims;

namespace SecureLanChat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IUserService _userService;
        private readonly IMessageService _messageService;
        private readonly IEncryptionService _encryptionService;
        private readonly IAESEncryptionService _aesEncryptionService;
        private readonly IKeyStorageService _keyStorageService;
        private readonly INotificationService _notificationService;
        private readonly ILoggingService _loggingService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            IUserService userService,
            IMessageService messageService,
            IEncryptionService encryptionService,
            IAESEncryptionService aesEncryptionService,
            IKeyStorageService keyStorageService,
            INotificationService notificationService,
            ILoggingService loggingService,
            ILogger<ChatHub> logger)
        {
            _userService = userService;
            _messageService = messageService;
            _encryptionService = encryptionService;
            _aesEncryptionService = aesEncryptionService;
            _keyStorageService = keyStorageService;
            _notificationService = notificationService;
            _loggingService = loggingService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
                
                // Get user ID from query string or headers
                var userId = GetUserIdFromContext();
                if (!string.IsNullOrEmpty(userId))
                {
                    // Update user status to online
                    await _userService.UpdateUserStatusAsync(userId, true);
                    
                    // Add user to group for notifications
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                    
                    // Notify other users about new online user
                    await NotifyUserOnlineAsync(userId);
                    
                    _loggingService.LogUserConnection(userId, Context.ConnectionId, true);
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync for connection {ConnectionId}", Context.ConnectionId);
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
                
                var userId = GetUserIdFromContext();
                if (!string.IsNullOrEmpty(userId))
                {
                    // Update user status to offline
                    await _userService.UpdateUserStatusAsync(userId, false);
                    
                    // Remove user from group
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
                    
                    // Notify other users about user going offline
                    await NotifyUserOfflineAsync(userId);
                    
                    _loggingService.LogUserConnection(userId, Context.ConnectionId, false);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync for connection {ConnectionId}", Context.ConnectionId);
            }
        }

        public async Task JoinChat(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User ID is required");
                    return;
                }

                _logger.LogInformation("User {UserId} joining chat", userId);

                // Validate user exists
                if (!await _userService.ValidateUserAsync(userId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid user");
                    return;
                }

                // Update user status
                await _userService.UpdateUserStatusAsync(userId, true);
                
                // Add to user group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                // Get online users and send to caller
                var onlineUsers = await _userService.GetOnlineUsersAsync();
                await Clients.Caller.SendAsync("OnlineUsers", onlineUsers.Select(u => new
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    IsOnline = u.IsOnline,
                    LastSeen = u.LastSeen
                }));

                // Notify others about user joining
                await NotifyUserOnlineAsync(userId);

                _loggingService.LogUserAction(userId, "join_chat", "User joined chat");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JoinChat for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to join chat");
            }
        }

        public async Task LeaveChat(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User ID is required");
                    return;
                }

                _logger.LogInformation("User {UserId} leaving chat", userId);

                // Update user status
                await _userService.UpdateUserStatusAsync(userId, false);
                
                // Remove from user group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                // Notify others about user leaving
                await NotifyUserOfflineAsync(userId);

                _loggingService.LogUserAction(userId, "leave_chat", "User left chat");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LeaveChat for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to leave chat");
            }
        }

        public async Task SendMessage(string senderId, string receiverId, string encryptedMessage, string messageType = "text")
        {
            try
            {
                if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(encryptedMessage))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid message data");
                    return;
                }

                _logger.LogInformation("Sending message from {SenderId} to {ReceiverId}", senderId, receiverId);

                // Validate sender exists
                if (!await _userService.ValidateUserAsync(senderId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid sender");
                    return;
                }

                // Get session AES key for sender
                var aesKey = await _keyStorageService.GetSessionKeyAsync(senderId, Context.ConnectionId);
                
                // Decrypt message to validate and log
                var decryptedMessage = await _aesEncryptionService.DecryptMessageAsync(
                    new EncryptedMessage { Content = encryptedMessage, IV = "" }, aesKey);

                // Create message record
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = Guid.Parse(senderId),
                    ReceiverId = string.IsNullOrEmpty(receiverId) ? null : Guid.Parse(receiverId),
                    Content = encryptedMessage,
                    IV = "", // Will be set by encryption service
                    MessageType = Enum.Parse<MessageType>(messageType, true),
                    Timestamp = DateTime.UtcNow
                };

                // Save message to database
                await _messageService.SaveMessageAsync(message);

                if (string.IsNullOrEmpty(receiverId))
                {
                    // Broadcast message to all users
                    await Clients.All.SendAsync("ReceiveMessage", new
                    {
                        Id = message.Id.ToString(),
                        SenderId = senderId,
                        ReceiverId = (string?)null,
                        Content = encryptedMessage,
                        MessageType = messageType,
                        Timestamp = message.Timestamp
                    });

                    _loggingService.LogMessageSent(senderId, "broadcast", true, messageType);
                }
                else
                {
                    // Send private message to specific user
                    await Clients.Group($"user_{receiverId}").SendAsync("ReceiveMessage", new
                    {
                        Id = message.Id.ToString(),
                        SenderId = senderId,
                        ReceiverId = receiverId,
                        Content = encryptedMessage,
                        MessageType = messageType,
                        Timestamp = message.Timestamp
                    });

                    _loggingService.LogMessageSent(senderId, receiverId, false, messageType);
                }

                // Send confirmation to sender
                await Clients.Caller.SendAsync("MessageSent", new
                {
                    Id = message.Id.ToString(),
                    Timestamp = message.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessage from {SenderId} to {ReceiverId}", senderId, receiverId);
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        public async Task GetMessageHistory(string userId, string? otherUserId = null, int limit = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("Error", "User ID is required");
                    return;
                }

                _logger.LogDebug("Getting message history for user {UserId}", userId);

                var messages = await _messageService.GetMessageHistoryAsync(userId, otherUserId, limit);
                
                await Clients.Caller.SendAsync("MessageHistory", messages.Select(m => new
                {
                    Id = m.Id.ToString(),
                    SenderId = m.SenderId.ToString(),
                    ReceiverId = m.ReceiverId?.ToString(),
                    Content = m.Content,
                    MessageType = m.MessageType.ToString(),
                    Timestamp = m.Timestamp
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMessageHistory for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to get message history");
            }
        }

        public async Task ExchangeKeys(string userId, string clientPublicKey)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(clientPublicKey))
                {
                    await Clients.Caller.SendAsync("Error", "User ID and public key are required");
                    return;
                }

                _logger.LogInformation("Key exchange for user {UserId}", userId);

                // Exchange public keys
                var serverPublicKey = await _encryptionService.ExchangePublicKeyAsync(clientPublicKey);
                
                // Generate AES key for session
                var aesKey = await _encryptionService.GenerateAESKeyAsync();
                
                // Store session key
                await _keyStorageService.StoreSessionKeyAsync(userId, Context.ConnectionId, aesKey);
                
                // Encrypt AES key with client's public key
                var encryptedAESKey = await _encryptionService.EncryptAESKeyAsync(aesKey, clientPublicKey);

                await Clients.Caller.SendAsync("KeysExchanged", new
                {
                    ServerPublicKey = serverPublicKey,
                    EncryptedAESKey = encryptedAESKey
                });

                _loggingService.LogEncryptionEvent("key_exchange", "exchange", true, "Keys exchanged successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExchangeKeys for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to exchange keys");
            }
        }

        public async Task GetOnlineUsers()
        {
            try
            {
                _logger.LogDebug("Getting online users");

                var onlineUsers = await _userService.GetOnlineUsersAsync();
                
                await Clients.Caller.SendAsync("OnlineUsers", onlineUsers.Select(u => new
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    IsOnline = u.IsOnline,
                    LastSeen = u.LastSeen
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOnlineUsers");
                await Clients.Caller.SendAsync("Error", "Failed to get online users");
            }
        }

        private async Task NotifyUserOnlineAsync(string userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                await Clients.All.SendAsync("UserOnline", new
                {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    IsOnline = true,
                    LastSeen = user.LastSeen
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying user online for {UserId}", userId);
            }
        }

        private async Task NotifyUserOfflineAsync(string userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                await Clients.All.SendAsync("UserOffline", new
                {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    IsOnline = false,
                    LastSeen = user.LastSeen
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying user offline for {UserId}", userId);
            }
        }

        private string? GetUserIdFromContext()
        {
            // Try to get user ID from query string
            if (Context.GetHttpContext()?.Request.Query.TryGetValue("userId", out var userId) == true)
            {
                return userId.FirstOrDefault();
            }

            // Try to get from headers
            if (Context.GetHttpContext()?.Request.Headers.TryGetValue("X-User-Id", out var headerUserId) == true)
            {
                return headerUserId.FirstOrDefault();
            }

            // Try to get from claims (if using authentication)
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
    }
}