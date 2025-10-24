using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SecureLanChat.Exceptions;
using SecureLanChat.Middleware;
using SecureLanChat.Models;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SecureLanChat.Tests.Middleware
{
    public class GlobalExceptionMiddlewareTests
    {
        private readonly Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock;
        private readonly RequestDelegate _next;
        private readonly GlobalExceptionMiddleware _middleware;

        public GlobalExceptionMiddlewareTests()
        {
            _loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
            _next = context => Task.CompletedTask;
            _middleware = new GlobalExceptionMiddleware(_next, _loggerMock.Object);
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleChatException_WithCorrectStatusCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var chatException = new UserNotFoundException("testuser");
            var next = new RequestDelegate(ctx => throw chatException);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            var responseBody = await GetResponseBody(context);
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal((int)HttpStatusCode.NotFound, errorResponse.StatusCode);
            Assert.Equal("User 'testuser' not found", errorResponse.Message);
            Assert.Equal("USER_NOT_FOUND", errorResponse.ErrorCode);
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleArgumentException_WithBadRequestStatusCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var argumentException = new ArgumentException("Invalid argument");
            var next = new RequestDelegate(ctx => throw argumentException);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);

            var responseBody = await GetResponseBody(context);
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResponse.StatusCode);
            Assert.Equal("Invalid argument provided", errorResponse.Message);
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleUnauthorizedAccessException_WithUnauthorizedStatusCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var unauthorizedException = new UnauthorizedAccessException("Access denied");
            var next = new RequestDelegate(ctx => throw unauthorizedException);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.Unauthorized, context.Response.StatusCode);

            var responseBody = await GetResponseBody(context);
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal((int)HttpStatusCode.Unauthorized, errorResponse.StatusCode);
            Assert.Equal("Unauthorized access", errorResponse.Message);
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleTimeoutException_WithRequestTimeoutStatusCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var timeoutException = new TimeoutException("Request timeout");
            var next = new RequestDelegate(ctx => throw timeoutException);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.RequestTimeout, context.Response.StatusCode);

            var responseBody = await GetResponseBody(context);
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal((int)HttpStatusCode.RequestTimeout, errorResponse.StatusCode);
            Assert.Equal("Request timeout", errorResponse.Message);
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleGenericException_WithInternalServerErrorStatusCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var genericException = new Exception("Generic error");
            var next = new RequestDelegate(ctx => throw genericException);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

            var responseBody = await GetResponseBody(context);
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal((int)HttpStatusCode.InternalServerError, errorResponse.StatusCode);
            Assert.Equal("An unexpected error occurred", errorResponse.Message);
        }

        [Fact]
        public async Task InvokeAsync_ShouldNotModifyResponse_WhenNoExceptionOccurs()
        {
            // Arrange
            var context = CreateHttpContext();
            var next = new RequestDelegate(ctx => Task.CompletedTask);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_ShouldIncludeRequestId_InErrorResponse()
        {
            // Arrange
            var context = CreateHttpContext();
            var requestId = "test-request-id";
            context.TraceIdentifier = requestId;

            var exception = new Exception("Test error");
            var next = new RequestDelegate(ctx => throw exception);

            var middleware = new GlobalExceptionMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            var responseBody = await GetResponseBody(context);
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal(requestId, errorResponse.RequestId);
        }

        private HttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private async Task<string> GetResponseBody(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            return await reader.ReadToEndAsync();
        }
    }
}
