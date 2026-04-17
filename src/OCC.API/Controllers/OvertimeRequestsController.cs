using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OvertimeRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<OvertimeRequestsController> _logger;

        public OvertimeRequestsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<OvertimeRequestsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/OvertimeRequests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OvertimeRequest>>> GetOvertimeRequests()
        {
            return await _context.OvertimeRequests
                .Include(r => r.Employee)
                .AsNoTracking()
                .ToListAsync();
        }

        // GET: api/OvertimeRequests/5
        [HttpGet("{id}")]
        public async Task<ActionResult<OvertimeRequest>> GetOvertimeRequest(Guid id)
        {
            var request = await _context.OvertimeRequests
                .Include(r => r.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();
            return request;
        }

        // POST: api/OvertimeRequests
        [HttpPost]
        public async Task<ActionResult<OvertimeRequest>> PostOvertimeRequest(OvertimeRequest request)
        {
            if (request.Id == Guid.Empty) request.Id = Guid.NewGuid();
            _context.OvertimeRequests.Add(request);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "OvertimeRequest", "Create", request.Id);
            // Notify Admins
            string employeeName = request.Employee != null ? $"{request.Employee.FirstName} {request.Employee.LastName}" : "Unknown Employee";
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New Overtime Request from {employeeName}");

            return CreatedAtAction("GetOvertimeRequest", new { id = request.Id }, request);
        }

        // PUT: api/OvertimeRequests/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOvertimeRequest(Guid id, OvertimeRequest request)
        {
            if (id != request.Id) return BadRequest();

            _context.Entry(request).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "OvertimeRequest", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OvertimeRequestExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/OvertimeRequests/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOvertimeRequest(Guid id)
        {
            var request = await _context.OvertimeRequests.FindAsync(id);
            if (request == null) return NotFound();

            _context.OvertimeRequests.Remove(request);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "OvertimeRequest", "Delete", id);

            return NoContent();
        }

        private bool OvertimeRequestExists(Guid id) => _context.OvertimeRequests.Any(e => e.Id == id);
    }
}
