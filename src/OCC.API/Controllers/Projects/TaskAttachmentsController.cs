using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskAttachmentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TaskAttachmentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("task/{taskId}")]
        public async Task<ActionResult<IEnumerable<TaskAttachment>>> GetAttachmentsForTask(Guid taskId)
        {
            return await _context.TaskAttachments
                                 .Where(a => a.TaskId == taskId)
                                 .OrderByDescending(a => a.UploadedAt)
                                 .ToListAsync();
        }

        [HttpPost("upload")]
        public async Task<ActionResult<TaskAttachment>> UploadAttachment([FromForm] TaskAttachment attachment, IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided.");
            }

            attachment.Id = Guid.NewGuid();
            attachment.UploadedAt = DateTime.UtcNow;

            // Ensure directory exists
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tasks", attachment.TaskId.ToString());
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Safe filename
            var safeFileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Set Metadata
            attachment.FileName = safeFileName;
            attachment.FilePath = $"/uploads/tasks/{attachment.TaskId}/{uniqueFileName}";
            attachment.FileSize = FormatFileSize(file.Length);

            _context.TaskAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            return Ok(attachment);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttachment(Guid id)
        {
            var attachment = await _context.TaskAttachments.FindAsync(id);
            if (attachment == null)
            {
                return NotFound();
            }

            // Try to delete physical file
            try 
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch(Exception ex)
            {
                // Verify log? For now proceed with DB delete
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }

            _context.TaskAttachments.Remove(attachment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
