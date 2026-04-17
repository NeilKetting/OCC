using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
    public class HseqDocumentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HseqDocumentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HseqDocument>>> GetDocuments()
        {
            return await _context.HseqDocuments.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<HseqDocument>> UploadDocument([FromForm] HseqDocument document, IFormFile? file)
        {
            document.Id = Guid.NewGuid();
            document.UploadDate = DateTime.UtcNow;

            if (file != null && file.Length > 0)
            {
                // Ensure directory exists
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "hseq", document.Category.ToString());
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Safe filename
                var fileName = $"{document.Id}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Set Metadata
                document.FilePath = $"/uploads/hseq/{document.Category}/{fileName}";
                document.FileSize = FormatFileSize(file.Length);
            }
            
            _context.HseqDocuments.Add(document);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetDocuments", new { id = document.Id }, document);
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            var document = await _context.HseqDocuments.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            _context.HseqDocuments.Remove(document);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
