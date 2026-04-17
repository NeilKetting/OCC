using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.Shared.DTOs; // We might need a DTO for stats

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HseqStatsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HseqStatsController> _logger;

        public HseqStatsController(AppDbContext context, ILogger<HseqStatsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<object>> GetDashboardStats()
        {
            // 1. Total Safe Man Hours (From Attendance)
            // Naive calc: Sum of hoursWorked in AttendanceRecords for current month/year?
            // Or total cumulative? Usually cumulative for Safe Hours.
            var totalHours = await _context.AttendanceRecords
                .SumAsync(a => a.HoursWorked);

            // 2. Incident Counts
            var incidents = await _context.Incidents
                .ToListAsync(); // Pull into mem for grouping

            var incidentsCount = incidents.Count;
            var nearMisses = incidents.Count(i => i.Type == Shared.Enums.IncidentType.NearMiss);
            var injuries = incidents.Count(i => i.Type == Shared.Enums.IncidentType.Injury);
            
            // 3. Audits
            var audits = await _context.HseqAudits
                .OrderByDescending(a => a.Date)
                .Take(5)
                .ToListAsync();
            
            var auditScores = audits.Select(a => new { a.SiteName, a.ActualScore, a.Date }).ToList();

            return Ok(new 
            {
                TotalSafeHours = totalHours, 
                IncidentsTotal = incidentsCount,
                NearMisses = nearMisses,
                Injuries = injuries,
                Environmentals = incidents.Count(i => i.Type == Shared.Enums.IncidentType.Environmental),
                RecentAuditScores = auditScores
            });
        }

        [HttpGet("history/{year?}")]
        public async Task<ActionResult<List<HseqSafeHourRecord>>> GetPerformanceHistory(int? year = null)
        {
            var targetYear = year ?? DateTime.Now.Year;
            
            // 1. Get Monthly Hours
            var monthlyHours = await _context.AttendanceRecords
                .Where(a => a.Date.Year == targetYear)
                .GroupBy(a => a.Date.Month)
                .Select(g => new { Month = g.Key, TotalHours = g.Sum(x => x.HoursWorked) })
                .ToDictionaryAsync(k => k.Month, v => v.TotalHours);

            // 2. Get Incidents
            var incidents = await _context.Incidents
                .Where(i => i.Date.Year == targetYear)
                .ToListAsync();

            var monthlyIncidents = incidents
                .GroupBy(i => i.Date.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3. Build Record for each month (up to current month if current year)
            var stats = new List<HseqSafeHourRecord>();
            var monthsToGenerate = (targetYear == DateTime.Now.Year) ? DateTime.Now.Month : 12;
            double cumulativeSafeHours = 0;

            for (int m = 1; m <= monthsToGenerate; m++)
            {
                var monthDate = new DateTime(targetYear, m, 1);
                var hours = monthlyHours.ContainsKey(m) ? monthlyHours[m] : 0;
                cumulativeSafeHours += hours;

                var monthIncidents = monthlyIncidents.ContainsKey(m) ? monthlyIncidents[m] : new List<Incident>();
                
                var hasIncidents = monthIncidents.Any();
                var nearMisses = monthIncidents.Count(i => i.Type == Shared.Enums.IncidentType.NearMiss);

                if (hasIncidents)
                {
                    _logger.LogInformation("Month {Month}/{Year} has {Count} incidents. First ID: {Id}, Description: {Desc}", 
                        m, targetYear, monthIncidents.Count, monthIncidents[0].Id, monthIncidents[0].Description);
                }

                stats.Add(new HseqSafeHourRecord
                {
                    Id = Guid.NewGuid(), // Generate ephemeral ID for UI
                    Month = monthDate,
                    SafeWorkHours = Math.Round(cumulativeSafeHours, 2),
                    IncidentReported = hasIncidents ? "Yes" : "No",
                    NearMisses = nearMisses,
                    Status = hasIncidents ? "Review" : "Closed",
                    ReportedBy = "System"
                });
            }

            return Ok(stats.OrderByDescending(s => s.Month).ToList());
        }

        [HttpPost("recalculate-hours")]
        public async Task<IActionResult> RecalculateHours()
        {
            var records = await _context.AttendanceRecords
                .Where(a => a.CheckInTime != null && a.CheckOutTime != null)
                .ToListAsync();

            int updatedCount = 0;
            foreach (var record in records)
            {
                if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue)
                {
                    var duration = record.CheckOutTime.Value - record.CheckInTime.Value;
                    if (duration.TotalHours > 0)
                    {
                        double lunchHours = (duration.TotalHours > 5) ? 0.75 : 0;
                        var hours = Math.Max(0, Math.Round(duration.TotalHours - lunchHours, 2));
                        
                        if (record.HoursWorked != hours)
                        {
                            record.HoursWorked = hours;
                            updatedCount++;
                        }
                    }
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = $"Recalculated hours for {records.Count} records. {updatedCount} records were updated.", UpdatedCount = updatedCount });
        }
    }
}
