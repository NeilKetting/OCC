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
    public class AppSettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<AppSettingsController> _logger;

        public AppSettingsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<AppSettingsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/AppSettings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppSetting>>> GetAppSettings()
        {
            try
            {
                return await _context.AppSettings.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving app settings");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/AppSettings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AppSetting>> GetAppSetting(Guid id)
        {
            try
            {
                var setting = await _context.AppSettings.FindAsync(id);
                if (setting == null) return NotFound();
                return setting;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving app setting {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/AppSettings
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AppSetting>> PostAppSetting(AppSetting setting)
        {
            try
            {
                if (setting.Id == Guid.Empty) setting.Id = Guid.NewGuid();
                _context.AppSettings.Add(setting);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AppSetting", "Create", setting.Id);

                return CreatedAtAction("GetAppSetting", new { id = setting.Id }, setting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating app setting");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/AppSettings/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutAppSetting(Guid id, AppSetting setting)
        {
            if (id != setting.Id) return BadRequest();
            _context.Entry(setting).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AppSetting", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AppSettingExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating app setting {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/AppSettings/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAppSetting(Guid id)
        {
            try
            {
                var setting = await _context.AppSettings.FindAsync(id);
                if (setting == null) return NotFound();
                _context.AppSettings.Remove(setting);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AppSetting", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting app setting {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool AppSettingExists(Guid id) => _context.AppSettings.Any(e => e.Id == id);
    }
}
