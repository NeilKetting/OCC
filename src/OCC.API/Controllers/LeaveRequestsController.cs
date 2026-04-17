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
    public class LeaveRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<LeaveRequestsController> _logger;

        public LeaveRequestsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<LeaveRequestsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/LeaveRequests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeaveRequest>>> GetLeaveRequests()
        {
            return await _context.LeaveRequests
                .Include(r => r.Employee)
                .AsNoTracking()
                .ToListAsync();
        }

        // GET: api/LeaveRequests/5
        [HttpGet("{id}")]
        public async Task<ActionResult<LeaveRequest>> GetLeaveRequest(Guid id)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();
            return request;
        }

        // POST: api/LeaveRequests
        [HttpPost]
        public async Task<ActionResult<LeaveRequest>> PostLeaveRequest(LeaveRequest request)
        {
            if (request.Id == Guid.Empty) request.Id = Guid.NewGuid();
            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Create", request.Id);
            // Notify Admins
            string employeeName = request.Employee != null ? $"{request.Employee.FirstName} {request.Employee.LastName}" : "Unknown Employee";
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New Leave Request from {employeeName}");

            return CreatedAtAction("GetLeaveRequest", new { id = request.Id }, request);
        }

        // PUT: api/LeaveRequests/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLeaveRequest(Guid id, LeaveRequest request)
        {
            if (id != request.Id) return BadRequest();

            _context.Entry(request).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LeaveRequestExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/LeaveRequests/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLeaveRequest(Guid id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null) return NotFound();

            _context.LeaveRequests.Remove(request);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Delete", id);

            return NoContent();
        }

        private bool LeaveRequestExists(Guid id) => _context.LeaveRequests.Any(e => e.Id == id);
    }
}
