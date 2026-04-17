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
    public class PublicHolidaysController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<PublicHolidaysController> _logger;

        public PublicHolidaysController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<PublicHolidaysController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/PublicHolidays
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PublicHoliday>>> GetPublicHolidays()
        {
            return await _context.PublicHolidays.AsNoTracking().ToListAsync();
        }

        // GET: api/PublicHolidays/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PublicHoliday>> GetPublicHoliday(Guid id)
        {
            var holiday = await _context.PublicHolidays.FindAsync(id);
            if (holiday == null) return NotFound();
            return holiday;
        }

        // POST: api/PublicHolidays
        [HttpPost]
        public async Task<ActionResult<PublicHoliday>> PostPublicHoliday(PublicHoliday holiday)
        {
            if (holiday.Id == Guid.Empty) holiday.Id = Guid.NewGuid();
            _context.PublicHolidays.Add(holiday);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "PublicHoliday", "Create", holiday.Id);

            return CreatedAtAction("GetPublicHoliday", new { id = holiday.Id }, holiday);
        }

        // PUT: api/PublicHolidays/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPublicHoliday(Guid id, PublicHoliday holiday)
        {
            if (id != holiday.Id) return BadRequest();

            _context.Entry(holiday).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "PublicHoliday", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PublicHolidayExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/PublicHolidays/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePublicHoliday(Guid id)
        {
            var holiday = await _context.PublicHolidays.FindAsync(id);
            if (holiday == null) return NotFound();

            _context.PublicHolidays.Remove(holiday);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "PublicHoliday", "Delete", id);

            return NoContent();
        }

        private bool PublicHolidayExists(Guid id) => _context.PublicHolidays.Any(e => e.Id == id);
    }
}
