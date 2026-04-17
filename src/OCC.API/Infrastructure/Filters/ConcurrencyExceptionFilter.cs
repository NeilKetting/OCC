using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace OCC.API.Infrastructure.Filters
{
    public class ConcurrencyExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ConcurrencyExceptionFilter> _logger;

        public ConcurrencyExceptionFilter(ILogger<ConcurrencyExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            if (context.Exception is DbUpdateConcurrencyException concurrencyEx)
            {
                _logger.LogWarning(concurrencyEx, "Concurrency conflict detected.");

                // For now, we return 409 Conflict. 
                // In a more advanced version, we could return the database values.
                var problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Title = "Concurrency Conflict",
                    Detail = "The record you attempted to edit was modified by another user or process. Please refresh and try again.",
                    Instance = context.HttpContext.Request.Path
                };

                context.Result = new ConflictObjectResult(problemDetails);
                context.ExceptionHandled = true;
            }
        }
    }
}
