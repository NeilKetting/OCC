using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;

namespace OCC.API.Infrastructure.Filters
{
    public class DatabaseExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<DatabaseExceptionFilter> _logger;

        private static readonly HashSet<int> ConnectionErrorNumbers = new()
        {
            -2,   // Timeout expired
            -1,   // Connection timeout
            2,    // Connection establish error
            11,   // General network error
            17,   // SQL Server does not exist or access denied
            26,   // Error Locating Server/Instance Specified
            40,   // Could not open connection
            53,   // Network path not found
            4060, // Cannot open database requested by login
            10053,// Connection aborted by host
            10054,// Connection reset by peer
            10060,// Connection timeout
            10061,// Connection refused
            18456 // Login failed for user
        };

        public DatabaseExceptionFilter(ILogger<DatabaseExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var sqlEx = GetSqlException(context.Exception);
            if (sqlEx != null)
            {
                if (ConnectionErrorNumbers.Contains(sqlEx.Number))
                {
                    _logger.LogCritical(context.Exception, "Database connection failure detected (SQL Error {ErrorNumber}): {Message}", sqlEx.Number, sqlEx.Message);

                    var problemDetails = new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.ServiceUnavailable,
                        Title = "Database Connection Failure",
                        Detail = "The database server is currently offline or unreachable. Please try again later.",
                        Instance = context.HttpContext.Request.Path
                    };

                    context.Result = new ObjectResult(problemDetails)
                    {
                        StatusCode = (int)HttpStatusCode.ServiceUnavailable
                    };
                    context.ExceptionHandled = true;
                }
                else
                {
                    _logger.LogError(context.Exception, "Database query exception occurred (SQL Error {ErrorNumber}): {Message}", sqlEx.Number, sqlEx.Message);

                    var problemDetails = new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.InternalServerError,
                        Title = "Database Query Failure",
                        Detail = "A database operation failed. Please contact your system administrator.",
                        Instance = context.HttpContext.Request.Path
                    };

                    context.Result = new ObjectResult(problemDetails)
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError
                    };
                    context.ExceptionHandled = true;
                }
            }
        }

        private SqlException? GetSqlException(Exception? ex)
        {
            while (ex != null)
            {
                if (ex is SqlException sqlEx)
                {
                    return sqlEx;
                }
                ex = ex.InnerException;
            }
            return null;
        }
    }
}
