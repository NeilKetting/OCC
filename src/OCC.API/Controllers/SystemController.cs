using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using System.Reflection;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous] // Allow access without token for initialization diagnostics
    public class SystemController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration; // Renamed from _config

        public SystemController(AppDbContext context, IWebHostEnvironment env, IConfiguration config)
        {
            _context = context;
            _env = env;
            _configuration = config; // Assigned to _configuration
        }

        [HttpGet("status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatus()
        {
            var status = new Dictionary<string, object>
            {
                { "Environment", _env.EnvironmentName },
                { "ServerTime", DateTime.Now },
                { "AssemblyVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown" }
            };

            try
            {
                var connString = _context.Database.GetConnectionString();
                status.Add("DatabaseConnection", MaskConnectionString(connString));

                // Check for applied migrations
                var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
                status.Add("AppliedMigrations", appliedMigrations);
                status.Add("MigrationCount", appliedMigrations.Count());

                status.Add("DatabaseStatus", "Connected");
            }
            catch (Exception ex)
            {
                status.Add("DatabaseStatus", "Error");
                status.Add("DatabaseError", ex.Message);
                
                try 
                {
                    var connString = _context.Database.GetConnectionString();
                    status.Add("DatabaseConnectionAttempted", MaskConnectionString(connString));
                }
                catch { }
            }

            // Diagnostic: List config keys related to DB (masked)
            var dbKeys = new Dictionary<string, string>();
            foreach (var kvp in _configuration.AsEnumerable())
            {
                if (kvp.Key.Contains("Connection", StringComparison.OrdinalIgnoreCase) || 
                    kvp.Key.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Contains("Server", StringComparison.OrdinalIgnoreCase))
                {
                    dbKeys[kvp.Key] = MaskString(kvp.Value);
                }
            }
            status.Add("ConfigDiagnostics", dbKeys);

            return Ok(status);
        }

        private string MaskString(string? val)
        {
            if (string.IsNullOrEmpty(val)) return "null/empty";
            if (val.Length <= 5) return "***";
            return val.Substring(0, 3) + "..." + val.Substring(val.Length - 2);
        }

        private string MaskConnectionString(string? conn)
        {
            if (string.IsNullOrEmpty(conn)) return "null/empty";
            var parts = conn.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var kvp = parts[i].Split('=');
                if (kvp.Length == 2)
                {
                    var key = kvp[0].Trim();
                    if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) || 
                        key.Equals("User ID", StringComparison.OrdinalIgnoreCase))
                    {
                        parts[i] = $"{key}=***";
                    }
                }
            }
            return string.Join(";", parts);
        }
    }
}
