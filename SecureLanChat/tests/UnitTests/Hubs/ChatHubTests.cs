using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SecureLanChat.Exceptions;
using SecureLanChat.Hubs;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Services;
using Xunit;

namespace SecureLanChat.Tests.Hubs
{
    public class ChatHubTests
    {
        private readonly Mock<IUserService> _userServiceMock;
        private readonly Mock<IMessageService> _messageServiceMock;
        private readonly Mock<IEncryptionService> _encryptionServiceMock;
        private readonly Mock<IAESEncryptionService> _aesEncryptionServiceMock;
        private readonly Mock<IKeyStorageService> _keyStorageServiceMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<ILoggingService> _loggingServiceMock;
        private readonly Mock<ILogger<ChatHub>> _loggerMock;
        private readonly Mock<IHubCallerClients> _clientsMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly ChatHub _chatHub;

        public ChatHubTests()
        {
            _userServiceMock = new Mock<IUserService>();
            _messageServiceMock = new Mock<IMessageService>();
            _encryptionServiceMock = new Mock<IEncryptionService>();
            _aesEncryptionServiceMock = new Mock<IAESEncryptionService>();
            _keyStorageServiceMock = new Mock<IKeyStorageService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _loggingServiceMock = new Mock<ILoggingService>();
            _loggerMock = new Mock<ILogger<ChatHub>>();
            _clientsMock = new Mock<IHubCallerClients>();
            _clientProxyMock = new Mock<IClientProxy>();
            _contextMock = new Mock<HubCallerContext>();

            _chatHub = new ChatHub(
                _userServiceMock.Object,
                _messageServiceMock.Object,
                _encryptionServiceMock.Object,
                _aesEncryptionServiceMock.Object,
                _keyStorageServiceMock.Object,
                _notificationServiceMock.Object,
                _loggingServiceMock.Object,
                _loggerMock.Object);

            // Setup context
            _contextMock.Setup(x => x.ConnectionId).Returns("test-connection-id");
            _contextMock.Setup(x => x.User).Returns((System.Security.Claims.ClaimsPrincipal)null);
            
            // Setup clients
            _clientsMock.Setup(x => x.Caller).Returns(_clientProxyMock.Object);
            _clientsMock.Setup(x => x.All).Returns(_clientProxyMock.Object);
            _clientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

            // Set context and clients
            _chatHub.Context = _contextMock.Object;
            _chatHub.Clients = _clientsMock.Object;
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldUpdateUserStatus_WhenUserIdProvided()
        {
            // Arrange
            _contextMock.Setup(x => x.GetHttpContext()).Returns(new Mock<HttpContext>().Object);
            _contextMock.Setup(x => x.GetHttpContext().Request.Query).Returns(new Mock<IQueryCollection>().Object);
            _contextMock.Setup(x => x.GetHttpContext().Request.Query.TryGetValue("userId", out It.Ref<string>.IsAny))
                .Returns(true)
                .Callback((string key, out string value) => value = "test-user-id");

            _userServiceMock.Setup(x => x.UpdateUserStatusAsync("test-user-id", true))
                .Returns(Task.CompletedTask);

            // Act
            await _chatHub.OnConnectedAsync();

            // Assert
            _userServiceMock.Verify(x => x.UpdateUserStatusAsync("test-user-id", true), Times.Once);
            _loggingServiceMock.Verify(x => x.LogUserConnection("test-user-id", "test-connection-id", true), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldUpdateUserStatus_WhenUserIdProvided()
        {
            // Arrange
            _contextMock.Setup(x => x.GetHttpContext()).Returns(new Mock<HttpContext>().Object);
            _contextMock.Setup(x => x.GetHttpContext().Request.Query).Returns(new Mock<IQueryCollection>().Object);
            _contextMock.Setup(x => x.GetHttpContext().Request.Query.TryGetValue("userId", out It.Ref<string>.IsAny))
                .Returns(true)
                .Callback((string key, out string value) => value = "test-user-id");

            _userServiceMock.Setup(x => x.UpdateUserStatusAsync("test-user-id", false))
                .Returns(Task.CompletedTask);

            // Act
            await _chatHub.OnDisconnectedAsync(null);

            // Assert
            _userServiceMock.Verify(x => x.UpdateUserStatusAsync("test-user-id", false), Times.Once);
            _loggingServiceMock.Verify(x => x.LogUserConnection("test-user-id", "test-connection-id", false), Times.Once);
        }

        [Fact]
        public async Task JoinChat_ShouldReturnError_WhenUserIdIsEmpty()
        {
            // Act
            await _chatHub.JoinChat("");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "User ID is required", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task JoinChat_ShouldReturnError_WhenUserIsInvalid()
        {
            // Arrange
            _userServiceMock.Setup(x => x.ValidateUserAsync("invalid-user"))
                .ReturnsAsync(false);

            // Act
            await _chatHub.JoinChat("invalid-user");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "Invalid user", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task JoinChat_ShouldJoinSuccessfully_WithValidUser()
        {
            // Arrange
            var onlineUsers = new List<User>
            {
                new User { Id = Guid.NewGuid(), Username = "user1", IsOnline = true, LastSeen = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), Username = "user2", IsOnline = true, LastSeen = DateTime.UtcNow }
            };

            _userServiceMock.Setup(x => x.ValidateUserAsync("test-user-id"))
                .ReturnsAsync(true);
            _userServiceMock.Setup(x => x.UpdateUserStatusAsync("test-user-id", true))
                .Returns(Task.CompletedTask);
            _userServiceMock.Setup(x => x.GetOnlineUsersAsync())
                .ReturnsAsync(onlineUsers);

            // Act
            await _chatHub.JoinChat("test-user-id");

            // Assert
            _userServiceMock.Verify(x => x.UpdateUserStatusAsync("test-user-id", true), Times.Once);
            _userServiceMock.Verify(x => x.GetOnlineUsersAsync(), Times.Once);
            _clientProxyMock.Verify(x => x.SendAsync("OnlineUsers", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
            _loggingServiceMock.Verify(x => x.LogUserAction("test-user-id", "join_chat", "User joined chat"), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ShouldReturnError_WhenSenderIdIsEmpty()
        {
            // Act
            await _chatHub.SendMessage("", "receiver-id", "encrypted-message");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "Invalid message data", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ShouldReturnError_WhenMessageIsEmpty()
        {
            // Act
            await _chatHub.SendMessage("sender-id", "receiver-id", "");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "Invalid message data", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ShouldReturnError_WhenSenderIsInvalid()
        {
            // Arrange
            _userServiceMock.Setup(x => x.ValidateUserAsync("invalid-sender"))
                .ReturnsAsync(false);

            // Act
            await _chatHub.SendMessage("invalid-sender", "receiver-id", "encrypted-message");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "Invalid sender", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ShouldSendBroadcastMessage_WhenReceiverIdIsEmpty()
        {
            // Arrange
            var senderId = "test-sender-id";
            var encryptedMessage = "encrypted-message";
            var aesKey = "test-aes-key";

            _userServiceMock.Setup(x => x.ValidateUserAsync(senderId))
                .ReturnsAsync(true);
            _keyStorageServiceMock.Setup(x => x.GetSessionKeyAsync(senderId, "test-connection-id"))
                .ReturnsAsync(aesKey);
            _aesEncryptionServiceMock.Setup(x => x.DecryptMessageAsync(It.IsAny<EncryptedMessage>(), aesKey))
                .ReturnsAsync("decrypted-message");
            _messageServiceMock.Setup(x => x.SaveMessageAsync(It.IsAny<Message>()))
                .ReturnsAsync(new Message());

            // Act
            await _chatHub.SendMessage(senderId, "", encryptedMessage);

            // Assert
            _messageServiceMock.Verify(x => x.SaveMessageAsync(It.IsAny<Message>()), Times.Once);
            _clientsMock.Verify(x => x.All, Times.Once);
            _clientProxyMock.Verify(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
            _loggingServiceMock.Verify(x => x.LogMessageSent(senderId, "broadcast", true, "text"), Times.Once);
        }

        [Fact]
        public async Task SendMessage_ShouldSendPrivateMessage_WhenReceiverIdIsProvided()
        {
            // Arrange
            var senderId = "test-sender-id";
            var receiverId = "test-receiver-id";
            var encryptedMessage = "encrypted-message";
            var aesKey = "test-aes-key";

            _userServiceMock.Setup(x => x.ValidateUserAsync(senderId))
                .ReturnsAsync(true);
            _keyStorageServiceMock.Setup(x => x.GetSessionKeyAsync(senderId, "test-connection-id"))
                .ReturnsAsync(aesKey);
            _aesEncryptionServiceMock.Setup(x => x.DecryptMessageAsync(It.IsAny<EncryptedMessage>(), aesKey))
                .ReturnsAsync("decrypted-message");
            _messageServiceMock.Setup(x => x.SaveMessageAsync(It.IsAny<Message>()))
                .ReturnsAsync(new Message());

            // Act
            await _chatHub.SendMessage(senderId, receiverId, encryptedMessage);

            // Assert
            _messageServiceMock.Verify(x => x.SaveMessageAsync(It.IsAny<Message>()), Times.Once);
            _clientsMock.Verify(x => x.Group($"user_{receiverId}"), Times.Once);
            _clientProxyMock.Verify(x => x.SendAsync("ReceiveMessage", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
            _loggingServiceMock.Verify(x => x.LogMessageSent(senderId, receiverId, false, "text"), Times.Once);
        }

        [Fact]
        public async Task GetMessageHistory_ShouldReturnError_WhenUserIdIsEmpty()
        {
            // Act
            await _chatHub.GetMessageHistory("");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "User ID is required", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMessageHistory_ShouldReturnMessageHistory_WithValidUserId()
        {
            // Arrange
            var userId = "test-user-id";
            var messages = new List<Message>
            {
                new Message { Id = Guid.NewGuid(), SenderId = Guid.NewGuid(), Content = "message1", Timestamp = DateTime.UtcNow },
                new Message { Id = Guid.NewGuid(), SenderId = Guid.NewGuid(), Content = "message2", Timestamp = DateTime.UtcNow }
            };

            _messageServiceMock.Setup(x => x.GetMessageHistoryAsync(userId, null, 50))
                .ReturnsAsync(messages);

            // Act
            await _chatHub.GetMessageHistory(userId);

            // Assert
            _messageServiceMock.Verify(x => x.GetMessageHistoryAsync(userId, null, 50), Times.Once);
            _clientProxyMock.Verify(x => x.SendAsync("MessageHistory", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExchangeKeys_ShouldReturnError_WhenUserIdIsEmpty()
        {
            // Act
            await _chatHub.ExchangeKeys("", "public-key");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "User ID and public key are required", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExchangeKeys_ShouldReturnError_WhenPublicKeyIsEmpty()
        {
            // Act
            await _chatHub.ExchangeKeys("user-id", "");

            // Assert
            _clientProxyMock.Verify(x => x.SendAsync("Error", "User ID and public key are required", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExchangeKeys_ShouldExchangeKeysSuccessfully_WithValidData()
        {
            // Arrange
            var userId = "test-user-id";
            var clientPublicKey = "client-public-key";
            var serverPublicKey = "server-public-key";
            var aesKey = "test-aes-key";
            var encryptedAESKey = "encrypted-aes-key";

            _encryptionServiceMock.Setup(x => x.ExchangePublicKeyAsync(clientPublicKey))
                .ReturnsAsync(serverPublicKey);
            _encryptionServiceMock.Setup(x => x.GenerateAESKeyAsync())
                .ReturnsAsync(aesKey);
            _keyStorageServiceMock.Setup(x => x.StoreSessionKeyAsync(userId, "test-connection-id", aesKey))
                .ReturnsAsync(aesKey);
            _encryptionServiceMock.Setup(x => x.EncryptAESKeyAsync(aesKey, clientPublicKey))
                .ReturnsAsync(encryptedAESKey);

            // Act
            await _chatHub.ExchangeKeys(userId, clientPublicKey);

            // Assert
            _encryptionServiceMock.Verify(x => x.ExchangePublicKeyAsync(clientPublicKey), Times.Once);
            _encryptionServiceMock.Verify(x => x.GenerateAESKeyAsync(), Times.Once);
            _keyStorageServiceMock.Verify(x => x.StoreSessionKeyAsync(userId, "test-connection-id", aesKey), Times.Once);
            _encryptionServiceMock.Verify(x => x.EncryptAESKeyAsync(aesKey, clientPublicKey), Times.Once);
            _clientProxyMock.Verify(x => x.SendAsync("KeysExchanged", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
            _loggingServiceMock.Verify(x => x.LogEncryptionEvent("key_exchange", "exchange", true, "Keys exchanged successfully"), Times.Once);
        }

        [Fact]
        public async Task GetOnlineUsers_ShouldReturnOnlineUsers()
        {
            // Arrange
            var onlineUsers = new List<User>
            {
                new User { Id = Guid.NewGuid(), Username = "user1", IsOnline = true, LastSeen = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), Username = "user2", IsOnline = true, LastSeen = DateTime.UtcNow }
            };

            _userServiceMock.Setup(x => x.GetOnlineUsersAsync())
                .ReturnsAsync(onlineUsers);

            // Act
            await _chatHub.GetOnlineUsers();

            // Assert
            _userServiceMock.Verify(x => x.GetOnlineUsersAsync(), Times.Once);
            _clientProxyMock.Verify(x => x.SendAsync("OnlineUsers", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
