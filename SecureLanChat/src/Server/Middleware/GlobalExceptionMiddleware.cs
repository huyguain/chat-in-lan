using System.Net;
using System.Text.Json;
using SecureLanChat.Models;

namespace SecureLanChat.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var response = new ErrorResponse();
            
            switch (exception)
            {
                case ChatException chatEx:
                    response = new ErrorResponse
                    {
                        StatusCode = (int)chatEx.StatusCode,
                        Message = chatEx.Message,
                        Details = chatEx.Details,
                        Timestamp = DateTime.UtcNow,
                        RequestId = context.TraceIdentifier
                    };
                    break;
                
                case ArgumentException argEx:
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Message = "Invalid argument provided",
                        Details = argEx.Message,
                        Timestamp = DateTime.UtcNow,
                        RequestId = context.TraceIdentifier
                    };
                    break;
                
                case UnauthorizedAccessException:
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.Unauthorized,
                        Message = "Unauthorized access",
                        Details = "You do not have permission to access this resource",
                        Timestamp = DateTime.UtcNow,
                        RequestId = context.TraceIdentifier
                    };
                    break;
                
                case TimeoutException:
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.RequestTimeout,
                        Message = "Request timeout",
                        Details = "The request took too long to process",
                        Timestamp = DateTime.UtcNow,
                        RequestId = context.TraceIdentifier
                    };
                    break;
                
                default:
                    response = new ErrorResponse
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError,
                        Message = "An unexpected error occurred",
                        Details = "Please try again later or contact support if the problem persists",
                        Timestamp = DateTime.UtcNow,
                        RequestId = context.TraceIdentifier
                    };
                    break;
            }

            context.Response.StatusCode = response.StatusCode;
            
            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await context.Response.WriteAsync(jsonResponse);
        }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
