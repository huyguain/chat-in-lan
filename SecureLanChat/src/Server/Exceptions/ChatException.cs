using System.Net;

namespace SecureLanChat.Exceptions
{
    public class ChatException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Details { get; }
        public string ErrorCode { get; }

        public ChatException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string details = null, string errorCode = null)
            : base(message)
        {
            StatusCode = statusCode;
            Details = details ?? message;
            ErrorCode = errorCode ?? GetDefaultErrorCode(statusCode);
        }

        public ChatException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string details = null, string errorCode = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Details = details ?? message;
            ErrorCode = errorCode ?? GetDefaultErrorCode(statusCode);
        }

        private static string GetDefaultErrorCode(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.BadRequest => "INVALID_REQUEST",
                HttpStatusCode.Unauthorized => "UNAUTHORIZED",
                HttpStatusCode.Forbidden => "FORBIDDEN",
                HttpStatusCode.NotFound => "NOT_FOUND",
                HttpStatusCode.Conflict => "CONFLICT",
                HttpStatusCode.RequestTimeout => "TIMEOUT",
                HttpStatusCode.InternalServerError => "INTERNAL_ERROR",
                _ => "UNKNOWN_ERROR"
            };
        }
    }

    public class UserNotFoundException : ChatException
    {
        public UserNotFoundException(string username) 
            : base($"User '{username}' not found", HttpStatusCode.NotFound, $"The user with username '{username}' does not exist", "USER_NOT_FOUND")
        {
        }

        public UserNotFoundException(Guid userId) 
            : base($"User with ID '{userId}' not found", HttpStatusCode.NotFound, $"The user with ID '{userId}' does not exist", "USER_NOT_FOUND")
        {
        }
    }

    public class UsernameAlreadyExistsException : ChatException
    {
        public UsernameAlreadyExistsException(string username) 
            : base($"Username '{username}' already exists", HttpStatusCode.Conflict, $"A user with the username '{username}' already exists. Please choose a different username.", "USERNAME_EXISTS")
        {
        }
    }

    public class InvalidCredentialsException : ChatException
    {
        public InvalidCredentialsException() 
            : base("Invalid credentials", HttpStatusCode.Unauthorized, "The provided credentials are invalid", "INVALID_CREDENTIALS")
        {
        }
    }

    public class SessionExpiredException : ChatException
    {
        public SessionExpiredException() 
            : base("Session expired", HttpStatusCode.Unauthorized, "Your session has expired. Please log in again.", "SESSION_EXPIRED")
        {
        }
    }

    public class EncryptionException : ChatException
    {
        public EncryptionException(string message, Exception innerException = null) 
            : base($"Encryption error: {message}", innerException, HttpStatusCode.InternalServerError, "An error occurred during encryption/decryption", "ENCRYPTION_ERROR")
        {
        }
    }

    public class MessageNotFoundException : ChatException
    {
        public MessageNotFoundException(Guid messageId) 
            : base($"Message with ID '{messageId}' not found", HttpStatusCode.NotFound, $"The message with ID '{messageId}' does not exist", "MESSAGE_NOT_FOUND")
        {
        }
    }

    public class DatabaseException : ChatException
    {
        public DatabaseException(string message, Exception innerException = null) 
            : base($"Database error: {message}", innerException, HttpStatusCode.InternalServerError, "An error occurred while accessing the database", "DATABASE_ERROR")
        {
        }
    }

    public class ValidationException : ChatException
    {
        public ValidationException(string message) 
            : base($"Validation error: {message}", HttpStatusCode.BadRequest, message, "VALIDATION_ERROR")
        {
        }
    }

    public class RateLimitExceededException : ChatException
    {
        public RateLimitExceededException() 
            : base("Rate limit exceeded", HttpStatusCode.TooManyRequests, "Too many requests. Please wait before trying again.", "RATE_LIMIT_EXCEEDED")
        {
        }
    }

    public class NotificationException : ChatException
    {
        public NotificationException(string message, Exception? innerException = null)
            : base(message, HttpStatusCode.InternalServerError, innerException?.Message, "NOTIFICATION_ERROR")
        {
        }
    }
}
