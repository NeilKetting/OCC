using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OCC.Shared.Framework;

namespace OCC.API.Middleware
{
    /// <summary>
    /// Global exception handling middleware enforcing OWASP safe error handling standards.
    /// Intercepts unhandled exceptions and outputs standardized ApiResponse payloads.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception encountered during request processing. Path: {Path}, TraceId: {TraceId}", context.Request.Path, context.TraceIdentifier);
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var errorMessage = _env.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred while processing your request. Please contact support.";

            var response = ApiResponse<object>.Fail(errorMessage);
            response.TraceId = context.TraceIdentifier;

            if (_env.IsDevelopment() && exception.StackTrace != null)
            {
                response.Errors.Add(exception.StackTrace);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(response, options);
            return context.Response.WriteAsync(json);
        }
    }
}
