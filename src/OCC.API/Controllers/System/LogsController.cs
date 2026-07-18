using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Linq;
using OCC.API.Data;
using Microsoft.EntityFrameworkCore;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;

        public LogsController(IWebHostEnvironment env, AppDbContext context)
        {
            _env = env;
            _context = context;
        }

        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping() => Ok(new { Message = "Logs API is alive", Timestamp = DateTime.UtcNow });

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetLatestServerLog([FromQuery] int lines = 1000)
        {
            try
            {
                var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(logsPath))
                {
                    return NotFound($"Logs directory not found at {logsPath}");
                }

                var directory = new DirectoryInfo(logsPath);
                var lastLogFile = directory.GetFiles("log-*.txt")
                                           .OrderByDescending(f => f.LastWriteTime)
                                           .FirstOrDefault();

                if (lastLogFile == null)
                {
                    return NotFound("No log files found.");
                }

                // Read file with sharing enabled
                using (var fs = new FileStream(lastLogFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    if (lines > 0)
                    {
                        var allLines = new List<string>();
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            allLines.Add(line);
                        }
                        
                        var content = string.Join("\n", allLines.TakeLast(lines));
                        return Ok(new 
                        { 
                            FileName = lastLogFile.Name, 
                            Timestamp = lastLogFile.LastWriteTime,
                            Content = content 
                        });
                    }
                    else
                    {
                        var content = sr.ReadToEnd();
                        return Ok(new 
                        { 
                            FileName = lastLogFile.Name, 
                            Timestamp = lastLogFile.LastWriteTime,
                            Content = content 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error reading logs: {ex.Message}");
            }
        }

        // ==================================================================================
        // CLIENT LOG UPLOAD MANAGEMENT
        // ==================================================================================

        [HttpGet("list")]
        [AllowAnonymous] // Relaxed for debugging live server 404
        public async Task<ActionResult<List<LogUploadRequest>>> GetUploadedLogs()
        {
            var logs = await _context.LogUploads.OrderByDescending(l => l.Timestamp).ToListAsync();
            return Ok(logs);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Developer,Admin")]
        public async Task<IActionResult> DeleteLog(Guid id)
        {
            var log = await _context.LogUploads.FindAsync(id);
            if (log == null) return NotFound();

            // Delete file
            if (!string.IsNullOrEmpty(log.FilePath) && System.IO.File.Exists(log.FilePath))
            {
                try
                {
                    System.IO.File.Delete(log.FilePath);
                    // Also try to delete the parent directory if empty
                    var dir = Path.GetDirectoryName(log.FilePath);
                    if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
                catch { /* Ignore file delete errors, just remove record */ }
            }

            _context.LogUploads.Remove(log);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("download/{id}")]
        //[Authorize(Roles = "Developer,Admin")] // Open for now to verify easily, or use token in browser
        [AllowAnonymous] 
        public async Task<IActionResult> DownloadLog(Guid id)
        {
            var log = await _context.LogUploads.FindAsync(id);
            if (log == null) return NotFound();

            if (!System.IO.File.Exists(log.FilePath)) return NotFound("Log file not found on server.");

            var stream = new FileStream(log.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, "application/zip", $"Logs_{log.UserName}_{log.Timestamp:yyyyMMdd_HHmm}.zip");
        }

        [HttpPost("upload")]
        [AllowAnonymous] 
        public async Task<IActionResult> UploadLogs([FromForm] LogUploadRequest request, IFormFile? file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded.");

                // Create secure directory structure: UserUploads/YYYY-MM-DD/{MachineName}_{UserName}_{Guid}/
                var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var uniqueSubfolder = $"{request.MachineName}_{request.UserName}_{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                // Sanitize filenames
                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    uniqueSubfolder = uniqueSubfolder.Replace(c, '_');
                }

                var uploadPath = Path.Combine(_env.ContentRootPath, "UserUploads", dateFolder, uniqueSubfolder);
                Directory.CreateDirectory(uploadPath);

                // 2. Save File
                var filePath = Path.Combine(uploadPath, "logs.zip");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 3. Save to DB
                request.Id = Guid.NewGuid(); // Ensure ID
                request.FilePath = filePath;
                request.Timestamp = DateTime.UtcNow; // Ensure server time

                _context.LogUploads.Add(request);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Logs uploaded successfully", Id = request.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
