using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SecureLanChat.Services;
using Xunit;

namespace SecureLanChat.Tests.Services
{
    public class LoggingServiceTests
    {
        private readonly Mock<ILogger<LoggingService>> _loggerMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly LoggingService _loggingService;

        public LoggingServiceTests()
        {
            _loggerMock = new Mock<ILogger<LoggingService>>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _loggingService = new LoggingService(_loggerMock.Object, _httpContextAccessorMock.Object);
        }

        [Fact]
        public void LogUserAction_ShouldLogWithCorrectParameters()
        {
            // Arrange
            var userId = "user123";
            var action = "login";
            var details = "User logged in successfully";

            // Act
            _loggingService.LogUserAction(userId, action, details);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"User Action - UserId: {userId}, Action: {action}, Details: {details}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogMessageSent_ShouldLogWithCorrectParameters()
        {
            // Arrange
            var senderId = "sender123";
            var receiverId = "receiver456";
            var isBroadcast = false;
            var messageType = "text";

            // Act
            _loggingService.LogMessageSent(senderId, receiverId, isBroadcast, messageType);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Message Sent - SenderId: {senderId}, ReceiverId: {receiverId}, IsBroadcast: {isBroadcast}, MessageType: {messageType}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogMessageSent_ShouldLogBroadcastMessage_WithNullReceiver()
        {
            // Arrange
            var senderId = "sender123";
            string receiverId = null;
            var isBroadcast = true;
            var messageType = "text";

            // Act
            _loggingService.LogMessageSent(senderId, receiverId, isBroadcast, messageType);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("ReceiverId: NULL")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogUserConnection_ShouldLogConnection_WithCorrectParameters()
        {
            // Arrange
            var userId = "user123";
            var connectionId = "conn456";
            var isConnected = true;

            // Act
            _loggingService.LogUserConnection(userId, connectionId, isConnected);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"User Connected - UserId: {userId}, ConnectionId: {connectionId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogUserConnection_ShouldLogDisconnection_WithCorrectParameters()
        {
            // Arrange
            var userId = "user123";
            var connectionId = "conn456";
            var isConnected = false;

            // Act
            _loggingService.LogUserConnection(userId, connectionId, isConnected);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"User Disconnected - UserId: {userId}, ConnectionId: {connectionId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogEncryptionEvent_ShouldLogSuccess_WithInformationLevel()
        {
            // Arrange
            var userId = "user123";
            var eventType = "key_generation";
            var success = true;
            var details = "RSA key pair generated successfully";

            // Act
            _loggingService.LogEncryptionEvent(userId, eventType, success, details);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Encryption Event - UserId: {userId}, EventType: {eventType}, Success: {success}, Details: {details}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogEncryptionEvent_ShouldLogFailure_WithErrorLevel()
        {
            // Arrange
            var userId = "user123";
            var eventType = "encryption";
            var success = false;
            var details = "Encryption failed due to invalid key";

            // Act
            _loggingService.LogEncryptionEvent(userId, eventType, success, details);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Encryption Event - UserId: {userId}, EventType: {eventType}, Success: {success}, Details: {details}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogDatabaseOperation_ShouldLogSuccess_WithInformationLevel()
        {
            // Arrange
            var operation = "SELECT";
            var table = "Users";
            var success = true;
            var duration = TimeSpan.FromMilliseconds(150);

            // Act
            _loggingService.LogDatabaseOperation(operation, table, success, duration);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Database Operation - Operation: {operation}, Table: {table}, Success: {success}, Duration: {duration.TotalMilliseconds}ms")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogDatabaseOperation_ShouldLogFailure_WithErrorLevel()
        {
            // Arrange
            var operation = "INSERT";
            var table = "Messages";
            var success = false;
            var duration = TimeSpan.FromMilliseconds(50);

            // Act
            _loggingService.LogDatabaseOperation(operation, table, success, duration);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Database Operation - Operation: {operation}, Table: {table}, Success: {success}, Duration: {duration.TotalMilliseconds}ms")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("CRITICAL", LogLevel.Critical)]
        [InlineData("ERROR", LogLevel.Error)]
        [InlineData("WARNING", LogLevel.Warning)]
        [InlineData("INFO", LogLevel.Information)]
        [InlineData("UNKNOWN", LogLevel.Information)]
        public void LogSecurityEvent_ShouldLogWithCorrectLevel(string severity, LogLevel expectedLevel)
        {
            // Arrange
            var eventType = "login_attempt";
            var userId = "user123";
            var details = "Multiple failed login attempts";

            // Act
            _loggingService.LogSecurityEvent(eventType, userId, details, severity);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Security Event - EventType: {eventType}, UserId: {userId}, Details: {details}, Severity: {severity}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogPerformanceMetric_ShouldLogWithCorrectParameters()
        {
            // Arrange
            var metricName = "response_time";
            var value = 150.5;
            var unit = "ms";

            // Act
            _loggingService.LogPerformanceMetric(metricName, value, unit);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Performance Metric - MetricName: {metricName}, Value: {value}, Unit: {unit}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogError_ShouldLogWithException()
        {
            // Arrange
            var message = "Test error message";
            var exception = new Exception("Test exception");
            var userId = "user123";
            var context = "test_context";

            // Act
            _loggingService.LogError(message, exception, userId, context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Error - Message: {message}, UserId: {userId}, Context: {context}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogWarning_ShouldLogWithCorrectParameters()
        {
            // Arrange
            var message = "Test warning message";
            var userId = "user123";
            var context = "test_context";

            // Act
            _loggingService.LogWarning(message, userId, context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Warning - Message: {message}, UserId: {userId}, Context: {context}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogInfo_ShouldLogWithCorrectParameters()
        {
            // Arrange
            var message = "Test info message";
            var userId = "user123";
            var context = "test_context";

            // Act
            _loggingService.LogInfo(message, userId, context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Info - Message: {message}, UserId: {userId}, Context: {context}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
