using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClientPortalController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientPortalController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves projects associated with the logged-in customer's email.
        /// </summary>
        [HttpGet("projects")]
        public async Task<IActionResult> GetClientProjects()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email);
            if (emailClaim == null || string.IsNullOrWhiteSpace(emailClaim.Value))
            {
                return Unauthorized("User email claim not found.");
            }

            var email = emailClaim.Value;

            // Find all Customer IDs where either the Customer email matches,
            // or one of the CustomerContact emails matches.
            var customerIds = await _context.Customers
                .Where(c => c.Email == email)
                .Select(c => c.Id)
                .ToListAsync();

            var contactCustomerIds = await _context.CustomerContacts
                .Where(cc => cc.Email == email)
                .Select(cc => cc.CustomerId)
                .ToListAsync();

            var allCustomerIds = customerIds.Concat(contactCustomerIds).Distinct().ToList();

            if (!allCustomerIds.Any())
            {
                // Return empty list if this user email is not registered to any customer
                return Ok(new List<object>());
            }

            // Get projects associated with these customers
            var projects = await _context.Projects
                .Where(p => p.CustomerId != null && allCustomerIds.Contains(p.CustomerId.Value))
                .Include(p => p.Tasks)
                .AsNoTracking()
                .ToListAsync();

            var result = projects.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.StartDate,
                p.EndDate,
                p.Status,
                Progress = Math.Round(p.Progress, 1),
                TotalTasks = p.TotalTaskCount,
                CompletedTasks = p.CompletedTaskCount,
                p.StreetLine1,
                p.City,
                p.Location
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Retrieves detailed information and tasks for a specific project, verifying client ownership.
        /// </summary>
        [HttpGet("projects/{projectId}")]
        public async Task<IActionResult> GetClientProjectDetails(Guid projectId)
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email);
            if (emailClaim == null || string.IsNullOrWhiteSpace(emailClaim.Value))
            {
                return Unauthorized("User email claim not found.");
            }

            var email = emailClaim.Value;

            var customerIds = await _context.Customers
                .Where(c => c.Email == email)
                .Select(c => c.Id)
                .ToListAsync();

            var contactCustomerIds = await _context.CustomerContacts
                .Where(cc => cc.Email == email)
                .Select(cc => cc.CustomerId)
                .ToListAsync();

            var allCustomerIds = customerIds.Concat(contactCustomerIds).Distinct().ToList();

            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound("Project not found.");
            }

            if (project.CustomerId == null || !allCustomerIds.Contains(project.CustomerId.Value))
            {
                return Forbid("You do not have access to this project.");
            }

            var result = new
            {
                project.Id,
                project.Name,
                project.Description,
                project.StartDate,
                project.EndDate,
                project.Status,
                Progress = Math.Round(project.Progress, 1),
                project.StreetLine1,
                project.StreetLine2,
                project.City,
                project.StateOrProvince,
                project.PostalCode,
                project.Country,
                project.Latitude,
                project.Longitude,
                Tasks = project.Tasks.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Description,
                    t.StartDate,
                    t.FinishDate,
                    Progress = t.PercentComplete,
                    t.IsComplete,
                    t.Status
                }).OrderBy(t => t.StartDate).ToList()
            };

            return Ok(result);
        }
    }
}
