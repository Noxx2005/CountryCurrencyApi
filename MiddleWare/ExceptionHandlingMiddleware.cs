using System.Net;
using System.Text.Json;

namespace CountryCurrencyApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // Create an anonymous type with explicit property names
            object response = exception switch
            {
                HttpRequestException httpEx => new
                {
                    error = "External data source unavailable",
                    details = httpEx.Message
                },
                _ => new { error = "Internal server error" }
            };

            context.Response.StatusCode = exception is HttpRequestException ?
                (int)HttpStatusCode.ServiceUnavailable :
                (int)HttpStatusCode.InternalServerError;

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}