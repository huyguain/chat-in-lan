namespace SecureLanChat.Interfaces
{
    public interface ILoggingService
    {
        void LogUserAction(string userId, string action, string? details = null);
        void LogMessageSent(string senderId, string receiverId, bool isBroadcast, string messageType = "text");
        void LogMessageReceived(string receiverId, string senderId, bool isBroadcast);
        void LogUserConnection(string userId, string connectionId, bool isConnected);
        void LogEncryptionEvent(string userId, string eventType, bool success, string? details = null);
        void LogDatabaseOperation(string operation, string table, bool success, TimeSpan duration);
        void LogSecurityEvent(string eventType, string userId, string details, string severity = "INFO");
        void LogPerformanceMetric(string metricName, double value, string unit = "ms");
        void LogError(string message, Exception? exception = null, string? userId = null, string? context = null);
        void LogWarning(string message, string? userId = null, string? context = null);
        void LogInfo(string message, string? userId = null, string? context = null);
    }
}

