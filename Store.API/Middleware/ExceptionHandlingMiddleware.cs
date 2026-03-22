using System.Net;
using System.Text.Json;

namespace Store.API.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        //delegate is can process an Http Request.
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        // define the serialization options on Write once to avoid to create a new object for option every request.
        private readonly JsonSerializerOptions s_WriteOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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
            catch(Exception ex)
            {
                if(ex is OperationCanceledException)
                {
                    context.Response.StatusCode = 499;
                    return;
                }
                _logger.LogError(ex, "Unhandled exception on {Method} - {Path}", context.Request.Method, context.Request.Path);
                await HandleExceptionAsync(context, ex);

            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                status = 500,
                error = "unhandled exception error occurred.",
                details = ex.Message// this for the development mode only the user should not see it in production mode
            };
            
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    response,
                    s_WriteOptions));
        }
    }
}
