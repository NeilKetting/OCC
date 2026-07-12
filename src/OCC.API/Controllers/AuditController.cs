using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<ActionResult<AuditLogsResponseDto>> GetAuditLogs(
            [FromQuery] string? search = null,
            [FromQuery] Guid? userId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 100)
        {
            try
            {
                var query = _context.AuditLogs.AsNoTracking();

                // 1. Filter by User
                if (userId.HasValue && userId.Value != Guid.Empty)
                {
                    if (userId.Value == Guid.Parse("00000000-0000-0000-0000-000000000001"))
                    {
                        query = query.Where(l => l.UserId.ToLower() == "system");
                    }
                    else
                    {
                        var userIdStr = userId.Value.ToString().ToLower();
                        query = query.Where(l => l.UserId.ToLower() == userIdStr);
                    }
                }

                // 2. Filter by Date Range
                if (startDate.HasValue)
                {
                    var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                    query = query.Where(l => l.Timestamp >= startUtc);
                }
                if (endDate.HasValue)
                {
                    var endUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                    query = query.Where(l => l.Timestamp <= endUtc);
                }

                // 3. Filter by Search Query
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var cleanSearch = search.Trim().ToLower();
                    query = query.Where(l => 
                        l.Action.ToLower().Contains(cleanSearch) || 
                        l.TableName.ToLower().Contains(cleanSearch) || 
                        l.RecordId.ToLower().Contains(cleanSearch) ||
                        l.UserId.ToLower().Contains(cleanSearch) ||
                        (l.NewValues != null && l.NewValues.ToLower().Contains(cleanSearch)) ||
                        (l.OldValues != null && l.OldValues.ToLower().Contains(cleanSearch))
                    );
                }

                // Calculate stats totals for all matching items
                var totalCount = await query.CountAsync();

                var createCount = await query.CountAsync(l => l.Action == "Create" || l.Action == "Login Failed" || l.Action == "Login Successful");
                var updateCount = await query.CountAsync(l => l.Action == "Update");
                var deleteCount = await query.CountAsync(l => l.Action == "Delete");

                // Retrieve current page items
                var items = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                return new AuditLogsResponseDto
                {
                    Items = items,
                    TotalCount = totalCount,
                    CreateCount = createCount,
                    UpdateCount = updateCount,
                    DeleteCount = deleteCount
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("count")]
        public async Task<ActionResult<int>> GetTotalCount()
        {
            try
            {
                return await _context.AuditLogs.CountAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
