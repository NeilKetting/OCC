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
    public class TimeRecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<TimeRecordsController> _logger;

        public TimeRecordsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<TimeRecordsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/TimeRecords
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TimeRecord>>> GetTimeRecords()
        {
            try
            {
                return await _context.TimeRecords.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving time records");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/TimeRecords/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TimeRecord>> GetTimeRecord(Guid id)
        {
            try
            {
                var record = await _context.TimeRecords.FindAsync(id);
                if (record == null) return NotFound();
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving time record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/TimeRecords
        [HttpPost]
        public async Task<ActionResult<TimeRecord>> PostTimeRecord(TimeRecord record)
        {
            try
            {
                if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
                _context.TimeRecords.Add(record);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TimeRecord", "Create", record.Id);
                
                return CreatedAtAction("GetTimeRecord", new { id = record.Id }, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating time record");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/TimeRecords/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTimeRecord(Guid id, TimeRecord record)
        {
            if (id != record.Id) return BadRequest();
            _context.Entry(record).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TimeRecord", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TimeRecordExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating time record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/TimeRecords/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTimeRecord(Guid id)
        {
            try
            {
                var record = await _context.TimeRecords.FindAsync(id);
                if (record == null) return NotFound();
                _context.TimeRecords.Remove(record);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TimeRecord", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting time record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool TimeRecordExists(Guid id) => _context.TimeRecords.Any(e => e.Id == id);
    }
}
