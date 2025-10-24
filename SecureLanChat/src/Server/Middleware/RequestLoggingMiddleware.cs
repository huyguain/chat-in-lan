using System.Diagnostics;
using System.Text;

namespace SecureLanChat.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = context.TraceIdentifier;

            // Log request
            await LogRequestAsync(context, requestId);

            // Capture response
            var originalResponseBody = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                // Log response
                await LogResponseAsync(context, requestId, stopwatch.Elapsed);

                // Restore response body
                await responseBody.CopyToAsync(originalResponseBody);
                context.Response.Body = originalResponseBody;
            }
        }

        private async Task LogRequestAsync(HttpContext context, string requestId)
        {
            var request = context.Request;
            var requestBody = await ReadRequestBodyAsync(request);

            _logger.LogInformation("Request - Method: {Method}, Path: {Path}, QueryString: {QueryString}, " +
                "ContentType: {ContentType}, ContentLength: {ContentLength}, UserAgent: {UserAgent}, " +
                "RemoteIP: {RemoteIP}, RequestId: {RequestId}, Body: {RequestBody}",
                request.Method,
                request.Path,
                request.QueryString,
                request.ContentType,
                request.ContentLength,
                request.Headers.UserAgent,
                context.Connection.RemoteIpAddress,
                requestId,
                requestBody);
        }

        private async Task LogResponseAsync(HttpContext context, string requestId, TimeSpan duration)
        {
            var response = context.Response;
            var responseBody = await ReadResponseBodyAsync(response);

            _logger.LogInformation("Response - StatusCode: {StatusCode}, ContentType: {ContentType}, " +
                "ContentLength: {ContentLength}, Duration: {Duration}ms, RequestId: {RequestId}, Body: {ResponseBody}",
                response.StatusCode,
                response.ContentType,
                response.ContentLength,
                duration.TotalMilliseconds,
                requestId,
                responseBody);
        }

        private async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            if (request.ContentLength == 0 || !request.Body.CanSeek)
                return string.Empty;

            request.EnableBuffering();
            request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            return body;
        }

        private async Task<string> ReadResponseBodyAsync(HttpResponse response)
        {
            if (response.ContentLength == 0 || !response.Body.CanSeek)
                return string.Empty;

            response.Body.Position = 0;
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            response.Body.Position = 0;

            return body;
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
