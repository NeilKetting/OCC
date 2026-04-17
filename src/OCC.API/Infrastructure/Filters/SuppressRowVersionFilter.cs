using Microsoft.AspNetCore.Mvc.Filters;

namespace OCC.API.Infrastructure.Filters
{
    /// <summary>
    /// This filter automatically removes any validation errors related to the "RowVersion" property.
    /// Since RowVersion is managed by the database and its EF Core metadata often conflicts with 
    /// ASP.NET Core's implicit model validation (marking it as required even if it's nullable in C#),
    /// this filter ensures that Missing RowVersion values never block an API request.
    /// </summary>
    public class SuppressRowVersionFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Specifically target "RowVersion" and "rowVersion" (for camelCase)
            if (context.ModelState.ContainsKey("RowVersion"))
            {
                context.ModelState.Remove("RowVersion");
            }

            if (context.ModelState.ContainsKey("rowVersion"))
            {
                context.ModelState.Remove("rowVersion");
            }
            
            // Handle nested RowVersion (e.g. "customer.RowVersion")
            var keysToRemove = context.ModelState.Keys
                .Where(k => k.EndsWith(".RowVersion", System.StringComparison.OrdinalIgnoreCase) 
                         || k.EndsWith(".rowVersion", System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                context.ModelState.Remove(key);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No action needed after execution
        }
    }
}
