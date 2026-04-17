using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.API.Hubs;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class TeamsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TeamsController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public TeamsController(AppDbContext context, ILogger<TeamsController> logger, IHubContext<Hubs.NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: api/Teams
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Team>>> GetTeams()
        {
            try
            {
                // Include members count or basic info if needed, but for now just the team
                return await _context.Teams.Include(t => t.Members).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving teams");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Teams/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Team>> GetTeam(Guid id)
        {
            try
            {
                var team = await _context.Teams
                    .Include(t => t.Members)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (team == null)
                {
                    return NotFound();
                }

                return team;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving team {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Teams
        [HttpPost]
        public async Task<ActionResult<Team>> PostTeam(Team team)
        {
            try
            {
                _context.Teams.Add(team);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Team", "Create", team.Id);

                return CreatedAtAction("GetTeam", new { id = team.Id }, team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Teams/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTeam(Guid id, Team team)
        {
            if (id != team.Id)
            {
                return BadRequest();
            }

            _context.Entry(team).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Team", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TeamExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating team {Id}", id);
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // DELETE: api/Teams/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            try
            {
                var team = await _context.Teams.FindAsync(id);
                if (team == null)
                {
                    return NotFound();
                }

                // Safe Deletion: Check for members
                var hasMembers = await _context.TeamMembers.AnyAsync(tm => tm.TeamId == id);
                if (hasMembers)
                {
                     return Conflict("Cannot delete team because it has associated members. Please remove all members first.");
                }

                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Team", "Delete", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool TeamExists(Guid id)
        {
            return _context.Teams.Any(e => e.Id == id);
        }
    }
}
