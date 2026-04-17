using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using OCC.API.Data;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public HealthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetStatus()
        {
            return Ok(new 
            { 
                Status = "Online", 
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production (Default)",
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("db-check")]
        public async Task<IActionResult> CheckDatabase()
        {
            var result = new
            {
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production (Default)",
                ConnectionStringFound = !string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")),
                ConnectionStringMasked = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection")),
                CanConnect = false,
                Error = ""
            };

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                var databaseName = "Unknown";

                if (!string.IsNullOrEmpty(connectionString))
                {
                    var builder = new System.Data.Common.DbConnectionStringBuilder();
                    builder.ConnectionString = connectionString;
                    
                    if (builder.TryGetValue("Initial Catalog", out var catalog))
                        databaseName = catalog as string;
                    else if (builder.TryGetValue("Database", out var db))
                        databaseName = db as string;
                }

                var canConnect = await _context.Database.CanConnectAsync();
                return Ok(new 
                { 
                    result.Environment, 
                    result.ConnectionStringFound, 
                    result.ConnectionStringMasked, 
                    DatabaseName = databaseName,
                    CanConnect = canConnect,
                    Message = canConnect ? "Successfully connected to the database." : "Database connection failed."
                });
            }
            catch (Exception ex)
            {
                return Ok(new 
                { 
                    result.Environment, 
                    result.ConnectionStringFound, 
                    result.ConnectionStringMasked, 
                    DatabaseName = "Error",
                    CanConnect = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        private string MaskConnectionString(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return "NULL";
            if (connectionString.Contains("Password="))
            {
                var parts = connectionString.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                    {
                        parts[i] = "Password=********";
                    }
                }
                return string.Join(";", parts);
            }
            return connectionString;
        }

        [HttpGet("log-check")]
        public IActionResult CheckLogging()
        {
            var basePath = AppContext.BaseDirectory;
            var logPath = Path.Combine(basePath, "logs");
            var testFilePath = Path.Combine(logPath, "test-write.txt");
            var diagnostics = new Dictionary<string, string>
            {
                { "BaseDirectory", basePath },
                { "LogDirectory", logPath },
                { "TestFilePath", testFilePath },
                { "DirectoryExists", "Checking..." },
                { "WriteTest", "Pending" },
                { "Error", "None" }
            };

            try
            {
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                    diagnostics["DirectoryExists"] = "Created";
                }
                else
                {
                    diagnostics["DirectoryExists"] = "Exists";
                }

                System.IO.File.WriteAllText(testFilePath, $"Test write at {DateTime.UtcNow}");
                diagnostics["WriteTest"] = "Success";
                
                // Cleanup
                // System.IO.File.Delete(testFilePath); 
            }
            catch (Exception ex)
            {
                diagnostics["WriteTest"] = "Failed";
                diagnostics["Error"] = ex.Message;
                diagnostics["StackTrace"] = ex.StackTrace ?? "No stack trace";
            }

            return Ok(diagnostics);
        }
    }
}
