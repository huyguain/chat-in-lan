# Logging và Error Handling

## Tổng quan

Hệ thống sử dụng Serilog cho structured logging và custom middleware cho error handling toàn diện. Tất cả logs được ghi vào console và file với format chuẩn.

## Cấu hình Logging

### 1. Serilog Configuration

Cấu hình trong `appsettings.json`:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### 2. Log Levels

- **Critical**: System failures, security breaches
- **Error**: Application errors, exceptions
- **Warning**: Potential issues, deprecated features
- **Information**: General application flow
- **Debug**: Detailed debugging information

## Error Handling

### 1. Global Exception Middleware

Tự động xử lý tất cả exceptions với response format chuẩn:

```csharp
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
    public string ErrorCode { get; set; }
    public DateTime Timestamp { get; set; }
    public string RequestId { get; set; }
}
```

### 2. Custom Exceptions

#### ChatException (Base)
- `UserNotFoundException` - User không tồn tại
- `UsernameAlreadyExistsException` - Username đã tồn tại
- `InvalidCredentialsException` - Thông tin đăng nhập sai
- `SessionExpiredException` - Session hết hạn
- `EncryptionException` - Lỗi mã hóa
- `MessageNotFoundException` - Tin nhắn không tồn tại
- `DatabaseException` - Lỗi database
- `ValidationException` - Lỗi validation
- `RateLimitExceededException` - Vượt quá giới hạn

### 3. HTTP Status Codes

- **400 Bad Request**: Invalid arguments, validation errors
- **401 Unauthorized**: Authentication required
- **403 Forbidden**: Access denied
- **404 Not Found**: Resource not found
- **409 Conflict**: Resource already exists
- **429 Too Many Requests**: Rate limit exceeded
- **500 Internal Server Error**: Unexpected errors
- **503 Service Unavailable**: Service down

## Logging Services

### 1. LoggingService

Service chuyên dụng cho logging với các methods:

```csharp
// User actions
LogUserAction(userId, action, details)

// Message operations
LogMessageSent(senderId, receiverId, isBroadcast, messageType)
LogMessageReceived(receiverId, senderId, isBroadcast)

// Connection management
LogUserConnection(userId, connectionId, isConnected)

// Encryption events
LogEncryptionEvent(userId, eventType, success, details)

// Database operations
LogDatabaseOperation(operation, table, success, duration)

// Security events
LogSecurityEvent(eventType, userId, details, severity)

// Performance metrics
LogPerformanceMetric(metricName, value, unit)

// General logging
LogError(message, exception, userId, context)
LogWarning(message, userId, context)
LogInfo(message, userId, context)
```

### 2. Request Logging Middleware

Tự động log tất cả HTTP requests và responses:

- Request method, path, query string
- Request headers và body
- Response status code và body
- Request duration
- Remote IP address

## Health Checks

### 1. Health Check Service

Comprehensive health monitoring:

```csharp
// Individual checks
CheckDatabaseHealthAsync()
CheckEncryptionServiceHealthAsync()
CheckSignalRHealthAsync()
CheckMemoryUsageAsync()
CheckDiskSpaceAsync()

// Overall health
CheckOverallHealthAsync()
```

### 2. Health Check Endpoints

- `GET /api/health` - Overall health
- `GET /api/health/database` - Database health
- `GET /api/health/encryption` - Encryption service health
- `GET /api/health/signalr` - SignalR health
- `GET /api/health/memory` - Memory usage
- `GET /api/health/disk` - Disk space

### 3. Health Check Response

```json
{
  "isHealthy": true,
  "status": "Healthy",
  "message": "All systems are operational",
  "duration": 150.5,
  "timestamp": "2024-12-24T10:30:00Z",
  "data": {
    "totalChecks": 5,
    "healthyChecks": 5,
    "unhealthyChecks": 0
  }
}
```

## Log Analysis

### 1. Log Patterns

#### User Actions
```
[10:30:15 INF] User Action - UserId: user123, Action: login, Details: User logged in successfully, RequestId: abc123
```

#### Message Operations
```
[10:30:20 INF] Message Sent - SenderId: user123, ReceiverId: user456, IsBroadcast: false, MessageType: text, RequestId: abc124
```

#### Security Events
```
[10:30:25 WRN] Security Event - EventType: failed_login, UserId: user123, Details: Multiple failed attempts, Severity: WARNING, RequestId: abc125
```

#### Performance Metrics
```
[10:30:30 INF] Performance Metric - MetricName: response_time, Value: 150.5, Unit: ms, RequestId: abc126
```

### 2. Log Monitoring

#### Key Metrics
- Request/response times
- Error rates by endpoint
- User activity patterns
- Security event frequency
- Database query performance

#### Alerts
- High error rates (>5%)
- Slow response times (>1s)
- Security events
- Memory usage >80%
- Disk space <10%

## Best Practices

### 1. Logging Guidelines

- **Structured Logging**: Sử dụng structured logging với properties
- **Request ID**: Luôn include request ID để trace
- **User Context**: Include user ID khi có thể
- **Sensitive Data**: Không log passwords, keys, personal data
- **Performance**: Log async để không block requests

### 2. Error Handling Guidelines

- **Specific Exceptions**: Sử dụng specific exceptions thay vì generic
- **Error Codes**: Include error codes cho client handling
- **User-Friendly Messages**: Messages dễ hiểu cho end users
- **Technical Details**: Chi tiết kỹ thuật trong logs, không trong response
- **Request Context**: Include request ID trong error responses

### 3. Monitoring Guidelines

- **Health Checks**: Regular health check monitoring
- **Alert Thresholds**: Set appropriate alert thresholds
- **Log Retention**: Configure log retention policies
- **Performance Baselines**: Establish performance baselines
- **Security Monitoring**: Monitor security events closely

## Troubleshooting

### 1. Common Issues

#### High Memory Usage
- Check for memory leaks in logging
- Review log retention settings
- Monitor log file sizes

#### Slow Performance
- Check database query logs
- Review request/response times
- Monitor encryption operations

#### Security Events
- Review failed login attempts
- Check for suspicious patterns
- Monitor encryption failures

### 2. Debug Commands

```bash
# View recent logs
tail -f logs/log-20241224.txt

# Search for errors
grep "ERROR" logs/log-*.txt

# Check health status
curl http://localhost:5000/api/health

# Monitor specific user
grep "user123" logs/log-*.txt
```

## Configuration

### 1. Environment Variables

```bash
# Log level
SERILOG__MINIMUMLEVEL__DEFAULT=Information

# Log file path
SERILOG__WRITETO__1__ARGS__PATH=logs/app-.log

# Health check timeout
HEALTHCHECK__TIMEOUT=30
```

### 2. Production Settings

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "SecureLanChat": "Information"
      }
    }
  }
}
```
