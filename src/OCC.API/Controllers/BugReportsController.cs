using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Hubs;
// using OCC.Shared.Extensions; // Assuming extension is not available, using Substring safe check

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BugReportsController : ControllerBase
    {
        #region Fields & Constructor

        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public BugReportsController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        #endregion

        #region Public Methods

        // GET: api/BugReports
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BugReport>>> GetBugReports([FromQuery] bool includeArchived = false)
        {
            if (!HasViewAccess(out bool isDev))
            {
                return Forbid();
            }

            var query = _context.BugReports.AsQueryable();

            // Normal users don't see Closed bugs in their active list to keep it clean
            // If includeArchived is true (dev only), they see everything
            if (!isDev || !includeArchived)
            {
                query = query.Where(b => b.Status != "Closed" && b.Status != "Resolved");
            }

            var list = await query
                .OrderByDescending(b => b.ReportedDate)
                .Select(b => new BugReport
                {
                    Id = b.Id,
                    Type = b.Type,
                    ReporterId = b.ReporterId,
                    ReporterName = b.ReporterName,
                    ReportedDate = b.ReportedDate,
                    ViewName = b.ViewName,
                    Description = b.Description,
                    Status = b.Status,
                    AdminComments = b.AdminComments,
                    CreatedAtUtc = b.CreatedAtUtc,
                    IsActive = b.IsActive
                    // ScreenshotBase64 and Comments are explicitly excluded here
                })
                .AsNoTracking()
                .ToListAsync();

            return list;
        }

        // GET: api/BugReports/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<BugReport>> GetBugReport(Guid id)
        {
            if (!HasViewAccess(out _)) return Forbid();

            var bugReport = await _context.BugReports
                .Include(b => b.Comments)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bugReport == null)
            {
                return NotFound();
            }

            return bugReport;
        }

        // POST: api/BugReports
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<BugReport>> PostBugReport(BugReport bugReport)
        {
            if (!HasViewAccess(out _)) return StatusCode(StatusCodes.Status403Forbidden, "Only Admin, Office, and Site Managers can report bugs.");

            bugReport.ReportedDate = DateTime.UtcNow;
            
            // Ensure ID is set
            if (bugReport.Id == Guid.Empty) bugReport.Id = Guid.NewGuid();

            _context.BugReports.Add(bugReport);
            await _context.SaveChangesAsync();

            // Notify Dev (Neil) about new feedback
            try
            {
                var prefix = bugReport.Type switch {
                    BugReportType.Bug => "🚨 New BUG",
                    BugReportType.Suggestion => "💡 New SUGGESTION",
                    BugReportType.Question => "❓ New QUESTION",
                    _ => "📝 New FEEDBACK"
                };
                string message = $"{prefix} from {bugReport.ReporterName} on {bugReport.ViewName}";
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
            }
            catch { /* Ignore notification errors */ }

            return CreatedAtAction("GetBugReport", new { id = bugReport.Id }, bugReport);
        }

        // DELETE: api/BugReports/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteBugReport(Guid id)
        {
            if (!IsNeilDev())
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Only the Developer (Neil) can delete bug reports.");
            }

            var bugReport = await _context.BugReports.FindAsync(id);
            if (bugReport == null)
            {
                return NotFound();
            }

            _context.BugReports.Remove(bugReport);
            await _context.SaveChangesAsync();

            // Broadcast real-time update
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "BugReport", "Delete", id);

            return NoContent();
        }

        // POST: api/BugReports/5/comments
        [HttpPost("{id}/comments")]
        [Authorize]
        public async Task<ActionResult<BugComment>> PostBugComment(Guid id, [FromQuery] string? status, BugComment comment)
        {
            var bugReport = await _context.BugReports.FindAsync(id);
            if (bugReport == null)
            {
                return NotFound();
            }

            // REPLY PERMISSION: Neil OR the original Reporter
            var isDev = IsNeilDev();
            var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant();
            var isReporter = false;
            
            // Check if current user is the reporter
            if (bugReport.ReporterId.HasValue && User.FindFirst(ClaimTypes.NameIdentifier)?.Value == bugReport.ReporterId.ToString())
            {
                isReporter = true;
            }
            // Fallback: check by name matches? No, rely on ID or Email if stored.
            // Earlier PostBugReport set ReporterId from Auth. 
            // Also check if ReporterId matches the sub claim.
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid) && uid == bugReport.ReporterId)
            {
                isReporter = true;
            }

            if (!isDev && !isReporter)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Only the Developer (Neil) or the original Reporter can comment on this bug.");
            }

            comment.BugReportId = id;
            comment.CreatedAtUtc = DateTime.UtcNow;
            
            // Ensure Author Details
            if (string.IsNullOrEmpty(comment.AuthorName))
            {
                comment.AuthorName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? (isDev ? "Neil Ketting" : "User");
            }
            if (string.IsNullOrEmpty(comment.AuthorEmail))
            {
                comment.AuthorEmail = currentUserEmail ?? string.Empty;
            }

            _context.BugComments.Add(comment);

            if (!string.IsNullOrEmpty(status))
            {
                // Validate Status? (Open, Resolved, Closed)
                bugReport.Status = status;
                _context.Entry(bugReport).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            // Real-time broadcast for comments and status changes
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "BugReport", "Update", id);

            // Notify UI...
            // If Dev replied -> Notify Reporter
            // If Reporter replied -> Notify Dev (Neil)
            
            try
            {
                string message = "";
                if (isDev && bugReport.ReporterId.HasValue)
                {
                     var desc = bugReport.Description.Length > 20 ? bugReport.Description.Substring(0, 20) : bugReport.Description;
                     message = $"Update on Bug Report: {desc}... (For: {bugReport.ReporterName})";
                }
                else if (isReporter)
                {
                     // Notify Neil
                     message = $"New Reply from {bugReport.ReporterName} on Bug Report: {bugReport.ViewName}";
                     // Since Neil might not be listening filters identically, we'll just broadcast.
                }

                if (!string.IsNullOrEmpty(message))
                {
                    var typeIcon = bugReport.Type switch {
                        BugReportType.Bug => "🐞",
                        BugReportType.Suggestion => "💡",
                        BugReportType.Question => "❓",
                        _ => "💬"
                    };
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"{typeIcon} {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending notification: {ex.Message}");
            }

            return Ok(comment);
        }

        // DELETE: api/BugReports/comments/5
        [HttpDelete("comments/{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteBugComment(Guid commentId)
        {
            if (!IsNeilDev())
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Only the Developer (Neil) can delete bug comments.");
            }

            var comment = await _context.BugComments.FindAsync(commentId);
            if (comment == null)
            {
                return NotFound();
            }

            var bugReportId = comment.BugReportId;
            _context.BugComments.Remove(comment);
            await _context.SaveChangesAsync();

            // Broadcast real-time update for the bug report to refresh comments
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "BugReport", "Update", bugReportId);

            return NoContent();
        }

        // GET: api/BugReports/solutions?q=example
        [HttpGet("solutions")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BugReport>>> GetSolutions([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new List<BugReport>());

            var query = _context.BugReports
                .Include(b => b.Comments)
                .Where(b => b.Status == "Fixed" || b.Status == "Resolved" || b.Status == "Closed")
                .AsQueryable();

            // Case-insensitive search in various fields
            var search = q.ToLower();
            var results = await query
                .Where(b => b.Description.ToLower().Contains(search) || 
                            b.ViewName.ToLower().Contains(search) || 
                            b.Comments.Any(c => c.Content.ToLower().Contains(search)))
                .OrderByDescending(b => b.ReportedDate)
                .Take(15)
                .AsNoTracking()
                .ToListAsync();

            // Strip heavy data for list view
            foreach (var r in results)
            {
                r.ScreenshotBase64 = null;
                // Keep only the last few comments as they likely contain the fix/explanation
                if (r.Comments.Count > 1) 
                    r.Comments = r.Comments.OrderBy(c => c.CreatedAtUtc).TakeLast(2).ToList();
            }

            return results;
        }

        #endregion

        #region Helpers

        private bool IsNeilDev()
        {
            // Check standard ClaimTypes.Email
            var email = User.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant();
            if (email == "neil@mdk.co.za") return true;

            // Check "email" claim fallback (common in some JWT configurations)
            var emailFallback = User.FindFirst("email")?.Value?.ToLowerInvariant();
            if (emailFallback == "neil@mdk.co.za") return true;

            // Check Name claim as fallback
            var nameEmail = User.FindFirst(ClaimTypes.Name)?.Value?.ToLowerInvariant();
            if (nameEmail == "neil@mdk.co.za") return true;

            return false;
        }

        private bool HasViewAccess(out bool isDev)
        {
            isDev = IsNeilDev();
            if (isDev) return true;

            // Check Roles for View/Create access
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            // Fallback for "role" claim
            if (string.IsNullOrEmpty(roleClaim))
            {
                roleClaim = User.FindFirst("role")?.Value;
            }

            if (string.IsNullOrEmpty(roleClaim)) return false;

            // Allow: Admin, Office, SiteManager
            var isAdmin = roleClaim == nameof(UserRole.Admin) || roleClaim == "0";
            var isOffice = roleClaim == nameof(UserRole.Office) || roleClaim == "1";
            var isSiteManager = roleClaim == nameof(UserRole.SiteManager) || roleClaim == "2";
            
            return isAdmin || isOffice || isSiteManager;
        }

        private bool BugReportExists(Guid id)
        {
            return _context.BugReports.Any(e => e.Id == id);
        }

        #endregion
    }
}
