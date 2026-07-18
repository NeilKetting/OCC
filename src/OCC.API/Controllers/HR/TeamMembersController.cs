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
    public class TeamMembersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TeamMembersController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public TeamMembersController(AppDbContext context, ILogger<TeamMembersController> logger, IHubContext<Hubs.NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: api/TeamMembers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TeamMember>>> GetTeamMembers()
        {
            return await _context.TeamMembers.ToListAsync();
        }
        
        // GET: api/TeamMembers/ByTeam/{teamId}
        [HttpGet("ByTeam/{teamId}")]
        public async Task<ActionResult<IEnumerable<TeamMember>>> GetByTeam(Guid teamId)
        {
            return await _context.TeamMembers.Where(tm => tm.TeamId == teamId).ToListAsync();
        }

        // POST: api/TeamMembers
        [HttpPost]
        public async Task<ActionResult<TeamMember>> PostTeamMember(TeamMember teamMember)
        {
            try
            {
                _context.TeamMembers.Add(teamMember);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TeamMember", "Create", teamMember.Id);
                // Also notify update of Team to ensure lists refresh count etc
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Team", "Update", teamMember.TeamId);

                return CreatedAtAction("GetTeamMember", new { id = teamMember.Id }, teamMember);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team member");
                return StatusCode(500, "Internal server error");
            }
        }
        
        // GET: api/TeamMembers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TeamMember>> GetTeamMember(Guid id)
        {
             var tm = await _context.TeamMembers.FindAsync(id);
             if (tm == null) return NotFound();
             return tm;
        }

        // DELETE: api/TeamMembers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeamMember(Guid id)
        {
            try
            {
                var teamMember = await _context.TeamMembers.FindAsync(id);
                if (teamMember == null)
                {
                    return NotFound();
                }

                _context.TeamMembers.Remove(teamMember);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "TeamMember", "Delete", id);
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "Team", "Update", teamMember.TeamId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team member {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
