using Microsoft.AspNetCore.Http;
using SecureLanChat.Interfaces;

namespace SecureLanChat.Services
{
    public class LoggingService : ILoggingService
    {
        private readonly ILogger<LoggingService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoggingService(ILogger<LoggingService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public void LogUserAction(string userId, string action, string? details = null)
        {
            _logger.LogInformation("User Action - UserId: {UserId}, Action: {Action}, Details: {Details}, RequestId: {RequestId}",
                userId, action, details, GetRequestId());
        }

        public void LogMessageSent(string senderId, string receiverId, bool isBroadcast, string messageType = "text")
        {
            _logger.LogInformation("Message Sent - SenderId: {SenderId}, ReceiverId: {ReceiverId}, IsBroadcast: {IsBroadcast}, MessageType: {MessageType}, RequestId: {RequestId}",
                senderId, receiverId ?? "NULL", isBroadcast, messageType, GetRequestId());
        }

        public void LogMessageReceived(string receiverId, string senderId, bool isBroadcast)
        {
            _logger.LogInformation("Message Received - ReceiverId: {ReceiverId}, SenderId: {SenderId}, IsBroadcast: {IsBroadcast}, RequestId: {RequestId}",
                receiverId, senderId, isBroadcast, GetRequestId());
        }

        public void LogUserConnection(string userId, string connectionId, bool isConnected)
        {
            var action = isConnected ? "Connected" : "Disconnected";
            _logger.LogInformation("User {Action} - UserId: {UserId}, ConnectionId: {ConnectionId}, RequestId: {RequestId}",
                action, userId, connectionId, GetRequestId());
        }

        public void LogEncryptionEvent(string userId, string eventType, bool success, string? details = null)
        {
            var logLevel = success ? LogLevel.Information : LogLevel.Error;
            _logger.Log(logLevel, "Encryption Event - UserId: {UserId}, EventType: {EventType}, Success: {Success}, Details: {Details}, RequestId: {RequestId}",
                userId, eventType, success, details, GetRequestId());
        }

        public void LogDatabaseOperation(string operation, string table, bool success, TimeSpan duration)
        {
            var logLevel = success ? LogLevel.Information : LogLevel.Error;
            _logger.Log(logLevel, "Database Operation - Operation: {Operation}, Table: {Table}, Success: {Success}, Duration: {Duration}ms, RequestId: {RequestId}",
                operation, table, success, duration.TotalMilliseconds, GetRequestId());
        }

        public void LogSecurityEvent(string eventType, string userId, string details, string severity = "INFO")
        {
            var logLevel = severity.ToUpper() switch
            {
                "CRITICAL" => LogLevel.Critical,
                "ERROR" => LogLevel.Error,
                "WARNING" => LogLevel.Warning,
                "INFO" => LogLevel.Information,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, "Security Event - EventType: {EventType}, UserId: {UserId}, Details: {Details}, Severity: {Severity}, RequestId: {RequestId}",
                eventType, userId, details, severity, GetRequestId());
        }

        public void LogPerformanceMetric(string metricName, double value, string unit = "ms")
        {
            _logger.LogInformation("Performance Metric - MetricName: {MetricName}, Value: {Value}, Unit: {Unit}, RequestId: {RequestId}",
                metricName, value, unit, GetRequestId());
        }

        public void LogError(string message, Exception? exception = null, string? userId = null, string? context = null)
        {
            _logger.LogError(exception, "Error - Message: {Message}, UserId: {UserId}, Context: {Context}, RequestId: {RequestId}",
                message, userId, context, GetRequestId());
        }

        public void LogWarning(string message, string? userId = null, string? context = null)
        {
            _logger.LogWarning("Warning - Message: {Message}, UserId: {UserId}, Context: {Context}, RequestId: {RequestId}",
                message, userId, context, GetRequestId());
        }

        public void LogInfo(string message, string? userId = null, string? context = null)
        {
            _logger.LogInformation("Info - Message: {Message}, UserId: {UserId}, Context: {Context}, RequestId: {RequestId}",
                message, userId, context, GetRequestId());
        }

        private string GetRequestId()
        {
            return _httpContextAccessor.HttpContext?.TraceIdentifier ?? "Unknown";
        }
    }
}
