using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditLog>>> GetAuditLogs()
        {
            return await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(100) // Limit to last 100 for now
                .ToListAsync();
        }
    }
}
