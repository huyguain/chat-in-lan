using Microsoft.AspNetCore.SignalR;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using SecureLanChat.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
                
                if (string.IsNullOrEmpty(aesKey))
                {
                    _logger.LogWarning("No AES key found for user {UserId} with connection {ConnectionId}", senderId, Context.ConnectionId);
                    await Clients.Caller.SendAsync("Error", "Encryption key not found. Please reconnect.");
                    return;
                }
                
                // Client sends IV+encrypted data combined as base64
                // Extract IV (first 16 bytes) and encrypted content (remaining bytes)
                string decryptedMessage = string.Empty;
                try
                {
                    var combinedBytes = Convert.FromBase64String(encryptedMessage);
                    if (combinedBytes.Length < 16)
                    {
                        _logger.LogError("Encrypted message too short: {Length} bytes (expected at least 16 for IV)", combinedBytes.Length);
                        await Clients.Caller.SendAsync("Error", "Invalid encrypted message format");
                        return;
                    }
                    
                    // Extract IV (first 16 bytes) and encrypted content (remaining)
                    var ivBytes = new byte[16];
                    Array.Copy(combinedBytes, 0, ivBytes, 0, 16);
                    var encryptedContentBytes = new byte[combinedBytes.Length - 16];
                    Array.Copy(combinedBytes, 16, encryptedContentBytes, 0, encryptedContentBytes.Length);
                    
                    var ivBase64 = Convert.ToBase64String(ivBytes);
                    var encryptedContentBase64 = Convert.ToBase64String(encryptedContentBytes);
                    
                    // Decrypt message from sender
                    var encryptedMsg = new EncryptedMessage 
                    { 
                        Content = encryptedContentBase64, 
                        IV = ivBase64 
                    };
                    decryptedMessage = await _aesEncryptionService.DecryptMessageAsync(encryptedMsg, aesKey);
                    _logger.LogDebug("Message decrypted successfully. Length: {Length}, Preview: {Preview}", 
                        decryptedMessage.Length, decryptedMessage.Substring(0, Math.Min(50, decryptedMessage.Length)));
                }
                catch (Exception decryptEx)
                {
                    _logger.LogError(decryptEx, "Failed to decrypt message from sender");
                    await Clients.Caller.SendAsync("Error", $"Failed to decrypt message: {decryptEx.Message}");
                    return;
                }

                if (string.IsNullOrEmpty(decryptedMessage))
                {
                    _logger.LogError("Decrypted message is null or empty");
                    await Clients.Caller.SendAsync("Error", "Failed to decrypt message");
                    return;
                }
                
                // Validate decrypted message is reasonable
                if (decryptedMessage.Length < 1)
                {
                    _logger.LogWarning("Decrypted message is too short: {Length}", decryptedMessage.Length);
                    await Clients.Caller.SendAsync("Error", "Message is too short");
                    return;
                }

                // Parse message type - map "text" to Broadcast or Private
                MessageType msgType;
                if (string.IsNullOrEmpty(messageType) || messageType.ToLower() == "text")
                {
                    // If no receiver or receiver is null/empty, it's a broadcast
                    msgType = string.IsNullOrEmpty(receiverId) ? MessageType.Broadcast : MessageType.Private;
                }
                else
                {
                    // Try to parse the message type
                    if (!Enum.TryParse<MessageType>(messageType, true, out msgType))
                    {
                        _logger.LogWarning("Invalid message type '{MessageType}', defaulting to Broadcast", messageType);
                        msgType = string.IsNullOrEmpty(receiverId) ? MessageType.Broadcast : MessageType.Private;
                    }
                }
                
                // Parse GUIDs with validation
                Guid senderGuid;
                if (!Guid.TryParse(senderId, out senderGuid))
                {
                    _logger.LogError("Invalid sender ID format: {SenderId}", senderId);
                    await Clients.Caller.SendAsync("Error", $"Invalid sender ID format: {senderId}");
                    return;
                }

                Guid? receiverGuid = null;
                if (!string.IsNullOrEmpty(receiverId))
                {
                    if (!Guid.TryParse(receiverId, out Guid parsedReceiverId))
                    {
                        _logger.LogError("Invalid receiver ID format: {ReceiverId}", receiverId);
                        await Clients.Caller.SendAsync("Error", $"Invalid receiver ID format: {receiverId}");
                        return;
                    }
                    receiverGuid = parsedReceiverId;
                }
                
                // Create message record - store plaintext for history viewing
                var now = DateTime.UtcNow;
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = senderGuid,
                    ReceiverId = receiverGuid,
                    Content = decryptedMessage, // Store plaintext for history (can view old messages)
                    IV = "", // Not needed for plaintext storage
                    MessageType = msgType,
                    CreatedAt = now,
                    Timestamp = now
                };

                _logger.LogDebug("Saving message to database. MessageId: {MessageId}, ContentLength: {Length} (plaintext)", 
                    message.Id, decryptedMessage.Length);

                // Save message to database
                try
                {
                    await _messageService.SaveMessageAsync(message);
                    _logger.LogDebug("Message saved successfully. MessageId: {MessageId}", message.Id);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to save message to database. MessageId: {MessageId}", message.Id);
                    _logger.LogError(saveEx, "Exception type: {ExceptionType}, Message: {Message}", saveEx.GetType().Name, saveEx.Message);
                    if (saveEx.InnerException != null)
                    {
                        _logger.LogError(saveEx.InnerException, "Inner exception: {Message}", saveEx.InnerException.Message);
                    }
                    
                    var errorMsg = $"Failed to save message: {saveEx.Message}";
                    if (saveEx.InnerException != null)
                    {
                        errorMsg += $" ({saveEx.InnerException.Message})";
                    }
                    errorMsg += $" [Type: {saveEx.GetType().Name}]";
                    
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                if (string.IsNullOrEmpty(receiverId))
                {
                    // Broadcast message to all users - re-encrypt for each receiver
                    _logger.LogDebug("Broadcasting message to all users");
                    
                    // Get all active sessions (online users)
                    var allSessions = await _keyStorageService.GetAllActiveSessionsAsync();
                    
                    foreach (var session in allSessions)
                    {
                        try
                        {
                            // Validate decrypted message is not empty
                            if (string.IsNullOrEmpty(decryptedMessage))
                            {
                                _logger.LogError("Decrypted message is null or empty for user {UserId}", session.UserId);
                                continue;
                            }
                            
                            // Re-encrypt message with receiver's AES key
                            if (string.IsNullOrEmpty(decryptedMessage))
                            {
                                _logger.LogError("Cannot encrypt null or empty message for broadcast to user {UserId}", session.UserId);
                                continue; // Skip this user
                            }
                            
                            _logger.LogDebug("Encrypting broadcast message for user {UserId}. Plaintext length: {Length}, content preview: {Preview}", 
                                session.UserId, decryptedMessage.Length, decryptedMessage.Substring(0, Math.Min(50, decryptedMessage.Length)));
                            var receiverEncrypted = await _aesEncryptionService.EncryptStringAsync(decryptedMessage, session.AESKey);
                            
                            _logger.LogDebug("Broadcast message encrypted for user {UserId}. Encrypted length: {Length}", 
                                session.UserId, receiverEncrypted.Length);
                            
                            if (receiverEncrypted.Length < 32)
                            {
                                _logger.LogError("Encrypted message too short for user {UserId}: {Length} bytes", 
                                    session.UserId, receiverEncrypted.Length);
                                continue; // Skip this user
                            }
                            
                            // Send to this user's connection
                            await Clients.Client(session.ConnectionId).SendAsync("ReceiveMessage", new
                            {
                                Id = message.Id.ToString(),
                                SenderId = senderId,
                                ReceiverId = (string?)null,
                                Content = receiverEncrypted,
                                MessageType = messageType,
                                Timestamp = message.Timestamp
                            });
                            
                            _logger.LogDebug("Broadcast message sent to user {UserId} via connection {ConnectionId}", 
                                session.UserId, session.ConnectionId);
                        }
                        catch (Exception encryptEx)
                        {
                            _logger.LogError(encryptEx, "Failed to encrypt broadcast message for user {UserId}", session.UserId);
                            // Continue with other users
                        }
                    }

                    _loggingService.LogMessageSent(senderId, "broadcast", true, messageType);
                }
                else
                {
                    // Send private message to specific user - re-encrypt with receiver's key
                    _logger.LogDebug("Sending private message to user {ReceiverId}", receiverId);
                    
                    // Get receiver's active session
                    var receiverSessions = await _keyStorageService.GetActiveSessionsByUserIdAsync(receiverId);
                    
                    if (!receiverSessions.Any())
                    {
                        _logger.LogWarning("No active session found for receiver {ReceiverId}", receiverId);
                        await Clients.Caller.SendAsync("Error", "Receiver is not online");
                        return;
                    }

                    foreach (var session in receiverSessions)
                    {
                        try
                        {
                            // Re-encrypt message with receiver's AES key
                            if (string.IsNullOrEmpty(decryptedMessage))
                            {
                                _logger.LogError("Cannot encrypt null or empty message for user {UserId}", session.UserId);
                                await Clients.Caller.SendAsync("Error", "Message content is empty");
                                return;
                            }
                            
                            _logger.LogDebug("Encrypting private message for user {UserId}. Plaintext length: {Length}", 
                                session.UserId, decryptedMessage.Length);
                            var receiverEncrypted = await _aesEncryptionService.EncryptStringAsync(decryptedMessage, session.AESKey);
                            
                            _logger.LogDebug("Private message encrypted for user {UserId}. Encrypted length: {Length}", 
                                session.UserId, receiverEncrypted.Length);
                            
                            if (receiverEncrypted.Length < 32)
                            {
                                _logger.LogError("Encrypted message too short for user {UserId}: {Length} bytes", 
                                    session.UserId, receiverEncrypted.Length);
                                await Clients.Caller.SendAsync("Error", $"Failed to encrypt message: encrypted data too short");
                                return;
                            }
                            
                            // Send to receiver's connection
                            await Clients.Client(session.ConnectionId).SendAsync("ReceiveMessage", new
                            {
                                Id = message.Id.ToString(),
                                SenderId = senderId,
                                ReceiverId = receiverId,
                                Content = receiverEncrypted,
                                MessageType = messageType,
                                Timestamp = message.Timestamp
                            });
                            
                            _logger.LogDebug("Private message sent to user {UserId} via connection {ConnectionId}", 
                                session.UserId, session.ConnectionId);
                        }
                        catch (Exception encryptEx)
                        {
                            _logger.LogError(encryptEx, "Failed to encrypt private message for user {UserId}", session.UserId);
                            await Clients.Caller.SendAsync("Error", $"Failed to encrypt message for receiver: {encryptEx.Message}");
                            return;
                        }
                    }

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
                _logger.LogError(ex, "Error in SendMessage from {SenderId} to {ReceiverId}: {Message}", senderId, receiverId, ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
                }
                
                // Send more detailed error to client
                var errorMessage = $"Failed to send message: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" ({ex.InnerException.Message})";
                }
                
                // Include error type for debugging
                errorMessage += $" [Type: {ex.GetType().Name}]";
                
                _logger.LogWarning("Sending error to client: {ErrorMessage}", errorMessage);
                await Clients.Caller.SendAsync("Error", errorMessage);
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

                _logger.LogDebug("Getting message history for user {UserId}, otherUserId: {OtherUserId}", userId, otherUserId);

                var messages = await _messageService.GetMessageHistoryAsync(userId, otherUserId, limit);
                
                // Get receiver's current AES key
                var receiverAESKey = await _keyStorageService.GetSessionKeyAsync(userId, Context.ConnectionId);
                
                if (string.IsNullOrEmpty(receiverAESKey))
                {
                    _logger.LogWarning("No AES key found for user {UserId} when loading message history", userId);
                    await Clients.Caller.SendAsync("Error", "Encryption key not found. Please reconnect.");
                    return;
                }

                var processedMessages = new List<object>();

                foreach (var message in messages)
                {
                    try
                    {
                        // Messages are now stored as plaintext in database (new format)
                        // Old messages might still be encrypted (for backward compatibility)
                        string encryptedContent;
                        string messageContent = message.Content;
                        
                        // Check if message looks like base64 encrypted data (old format)
                        // More reliable check: base64 strings are usually longer and don't contain common text characters
                        bool isEncrypted = false;
                        if (!string.IsNullOrEmpty(messageContent))
                        {
                            // Check if it's a valid base64 string that's likely encrypted
                            bool isBase64 = Regex.IsMatch(messageContent, @"^[A-Za-z0-9+/=]+$");
                            
                            // Base64 encrypted data characteristics:
                            // - No spaces, newlines, tabs (unless it's a very long message)
                            // - Length > 32 (minimum for IV + small encrypted content)
                            // - Doesn't contain common punctuation (unless it's part of base64 padding)
                            bool hasTextFeatures = messageContent.Contains(" ") || 
                                                  messageContent.Contains("\n") || 
                                                  messageContent.Contains("\t") ||
                                                  messageContent.Contains("!") ||
                                                  messageContent.Contains("?") ||
                                                  messageContent.Contains(",") ||
                                                  messageContent.Contains(".") ||
                                                  messageContent.Contains("'") ||
                                                  messageContent.Contains("\"");
                            
                            // If it's base64-like but doesn't have text features and is long enough, it's probably encrypted
                            isEncrypted = isBase64 && !hasTextFeatures && messageContent.Length > 32;
                            
                            _logger.LogDebug("Message {MessageId} content check: Length={Length}, IsBase64={IsBase64}, HasTextFeatures={HasText}, IsEncrypted={IsEnc}", 
                                message.Id, messageContent.Length, isBase64, hasTextFeatures, isEncrypted);
                        }
                        
                        if (isEncrypted)
                        {
                            // Old encrypted format - try to decrypt with sender's key
                            _logger.LogDebug("Attempting to decrypt old encrypted message {MessageId}", message.Id);
                            var senderIdStr = message.SenderId.ToString();
                            var senderSessions = await _keyStorageService.GetActiveSessionsByUserIdAsync(senderIdStr);
                            
                            if (senderSessions.Any())
                            {
                                try
                                {
                                    var senderKey = senderSessions.First().AESKey;
                                    var combinedBytes = Convert.FromBase64String(message.Content);
                                    if (combinedBytes.Length >= 16)
                                    {
                                        var ivBytes = new byte[16];
                                        Array.Copy(combinedBytes, 0, ivBytes, 0, 16);
                                        var encryptedContentBytes = new byte[combinedBytes.Length - 16];
                                        Array.Copy(combinedBytes, 16, encryptedContentBytes, 0, encryptedContentBytes.Length);
                                        
                                        var ivBase64 = Convert.ToBase64String(ivBytes);
                                        var encryptedContentBase64 = Convert.ToBase64String(encryptedContentBytes);
                                        
                                        var encryptedMsg = new EncryptedMessage 
                                        { 
                                            Content = encryptedContentBase64, 
                                            IV = ivBase64 
                                        };
                                        
                                        messageContent = await _aesEncryptionService.DecryptMessageAsync(encryptedMsg, senderKey);
                                        _logger.LogDebug("Successfully decrypted old encrypted message {MessageId}", message.Id);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Message {MessageId} content too short to decrypt: {Length} bytes", 
                                            message.Id, combinedBytes.Length);
                                        messageContent = "[Old message - invalid format]";
                                    }
                                }
                                catch (CryptographicException cryptoEx)
                                {
                                    // Padding errors indicate wrong key or corrupted data - expected for old messages
                                    _logger.LogDebug("Old message {MessageId} cannot be decrypted with current key (expected): {Error}", 
                                        message.Id, cryptoEx.Message);
                                    messageContent = "[Old message - cannot decrypt (key changed or expired)]";
                                }
                                catch (Exception decryptEx)
                                {
                                    _logger.LogDebug("Failed to decrypt old message {MessageId}: {Error}", 
                                        message.Id, decryptEx.Message);
                                    messageContent = "[Old message - unable to decrypt]";
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Cannot decrypt old message {MessageId} - sender offline", message.Id);
                                messageContent = "[Old message - sender offline]";
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Message {MessageId} is plaintext (new format), length: {Length}", 
                                message.Id, messageContent?.Length ?? 0);
                        }
                        
                        // Encrypt plaintext with receiver's current key for transmission
                        try
                        {
                            if (string.IsNullOrEmpty(messageContent))
                            {
                                _logger.LogWarning("Message {MessageId} content is null or empty, using placeholder", message.Id);
                                messageContent = "[Empty message]";
                            }
                            
                            encryptedContent = await _aesEncryptionService.EncryptStringAsync(messageContent, receiverAESKey);
                            _logger.LogDebug("Encrypted message {MessageId} with receiver's key for transmission. Encrypted length: {Length}", 
                                message.Id, encryptedContent.Length);
                        }
                        catch (Exception encryptEx)
                        {
                            _logger.LogError(encryptEx, "Failed to encrypt message {MessageId} for transmission", message.Id);
                            encryptedContent = "[Error encrypting message]";
                        }

                        processedMessages.Add(new
                        {
                            Id = message.Id.ToString(),
                            SenderId = message.SenderId.ToString(),
                            ReceiverId = message.ReceiverId?.ToString(),
                            Content = encryptedContent,
                            MessageType = message.MessageType.ToString(),
                            Timestamp = message.Timestamp
                        });
                    }
                    catch (Exception msgEx)
                    {
                        _logger.LogError(msgEx, "Failed to process message {MessageId} in history", message.Id);
                        // Skip this message or add with error indicator
                        processedMessages.Add(new
                        {
                            Id = message.Id.ToString(),
                            SenderId = message.SenderId.ToString(),
                            ReceiverId = message.ReceiverId?.ToString(),
                            Content = "[Unable to decrypt - old message]",
                            MessageType = message.MessageType.ToString(),
                            Timestamp = message.Timestamp
                        });
                    }
                }
                
                _logger.LogDebug("Sending {Count} processed messages to user {UserId}", processedMessages.Count, userId);
                await Clients.Caller.SendAsync("MessageHistory", processedMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMessageHistory for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to get message history: " + ex.Message);
            }
        }

        public async Task ExchangeKeys(string userId, string clientPublicKey)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(clientPublicKey))
                {
                    _logger.LogWarning("ExchangeKeys called with missing parameters. UserId: {UserId}, HasKey: {HasKey}", 
                        userId ?? "null", !string.IsNullOrEmpty(clientPublicKey));
                    await Clients.Caller.SendAsync("Error", "User ID and public key are required");
                    return;
                }

                _logger.LogInformation("Key exchange initiated for user {UserId}. Key length: {KeyLength}", 
                    userId, clientPublicKey.Length);

                // Exchange public keys
                _logger.LogDebug("Calling ExchangePublicKeyAsync...");
                var serverPublicKey = await _encryptionService.ExchangePublicKeyAsync(clientPublicKey);
                _logger.LogDebug("ExchangePublicKeyAsync completed. Server key length: {Length}", serverPublicKey.Length);
                
                // Generate AES key for session
                _logger.LogDebug("Generating AES key...");
                var aesKey = await _encryptionService.GenerateAESKeyAsync();
                _logger.LogDebug("AES key generated. Length: {Length}", aesKey.Length);
                
                // Store session key
                _logger.LogDebug("Storing session key...");
                await _keyStorageService.StoreSessionKeyAsync(userId, Context.ConnectionId, aesKey);
                _logger.LogDebug("Session key stored");
                
                // Encrypt AES key with client's public key
                _logger.LogDebug("Encrypting AES key with client public key...");
                var encryptedAESKey = await _encryptionService.EncryptAESKeyAsync(aesKey, clientPublicKey);
                _logger.LogDebug("AES key encrypted. Encrypted length: {Length}", encryptedAESKey.Length);

                _logger.LogInformation("Sending keys to client...");
                await Clients.Caller.SendAsync("KeysExchanged", new
                {
                    ServerPublicKey = serverPublicKey,
                    EncryptedAESKey = encryptedAESKey
                });
                _logger.LogInformation("Keys sent successfully");

                _loggingService.LogEncryptionEvent("key_exchange", "exchange", true, "Keys exchanged successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExchangeKeys for user {UserId}: {Message}", userId, ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                
                // Send more detailed error to client for debugging
                var errorMessage = $"Failed to exchange keys: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" ({ex.InnerException.Message})";
                }
                await Clients.Caller.SendAsync("Error", errorMessage);
            }
        }

        public async Task GetOnlineUsers()
        {
            try
            {
                _logger.LogDebug("Getting online users");

                var onlineUsers = await _userService.GetOnlineUsersAsync();
                
                await Clients.Caller.SendAsync("OnlineUsers", onlineUsers.Where(u => u != null).Select(u => new
                {
                    Id = u!.Id.ToString(),
                    Username = u.Username ?? "Unknown",
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
                if (user == null)
                {
                    _logger.LogWarning("Cannot notify user online - user {UserId} not found", userId);
                    return;
                }
                
                await Clients.All.SendAsync("UserOnline", new
                {
                    Id = user.Id.ToString(),
                    Username = user.Username ?? "Unknown",
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
                if (user == null)
                {
                    _logger.LogWarning("Cannot notify user offline - user {UserId} not found", userId);
                    return;
                }
                
                await Clients.All.SendAsync("UserOffline", new
                {
                    Id = user.Id.ToString(),
                    Username = user.Username ?? "Unknown",
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