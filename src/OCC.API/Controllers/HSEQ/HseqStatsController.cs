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
            // 1. Total Safe Man Hours (From Attendance since the last severe incident)
            var lastIncidentDate = await _context.Incidents
                .Where(i => i.Severity == OCC.Shared.Enums.IncidentSeverity.High || 
                           i.Severity == OCC.Shared.Enums.IncidentSeverity.Critical || 
                           i.Severity == OCC.Shared.Enums.IncidentSeverity.Fatality)
                .OrderByDescending(i => i.Date)
                .Select(i => (DateTime?)i.Date)
                .FirstOrDefaultAsync();

            double totalHours = 0;
            var recordsQuery = _context.AttendanceRecords.AsNoTracking();
            if (lastIncidentDate.HasValue)
            {
                recordsQuery = recordsQuery.Where(a => a.Date > lastIncidentDate.Value);
            }

            var records = await recordsQuery
                .Select(a => new { a.HoursWorked, a.CheckInTime, a.CheckOutTime })
                .ToListAsync();

            totalHours = records.Sum(a => 
            {
                if (a.CheckInTime.HasValue && a.CheckOutTime.HasValue)
                {
                    var duration = (a.CheckOutTime.Value - a.CheckInTime.Value).TotalHours;
                    return duration > 0 ? Math.Round(duration, 2) : 0;
                }
                return a.HoursWorked;
            });

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

        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<double>> GetProjectSafeHours(Guid projectId)
        {
            var lastIncidentDate = await _context.Incidents
                .Where(i => i.ProjectId == projectId && 
                           (i.Severity == OCC.Shared.Enums.IncidentSeverity.High || 
                            i.Severity == OCC.Shared.Enums.IncidentSeverity.Critical || 
                            i.Severity == OCC.Shared.Enums.IncidentSeverity.Fatality))
                .OrderByDescending(i => i.Date)
                .Select(i => (DateTime?)i.Date)
                .FirstOrDefaultAsync();

            double totalHours = 0;
            var recordsQuery = _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId &&
                           (a.Status == OCC.Shared.Models.AttendanceStatus.Present || 
                            a.Status == OCC.Shared.Models.AttendanceStatus.Late || 
                            a.Status == OCC.Shared.Models.AttendanceStatus.LeaveEarly));

            if (lastIncidentDate.HasValue)
            {
                recordsQuery = recordsQuery.Where(a => a.Date > lastIncidentDate.Value);
            }

            var records = await recordsQuery
                .Select(a => new { a.HoursWorked, a.CheckInTime, a.CheckOutTime })
                .ToListAsync();

            totalHours = records.Sum(a => 
            {
                if (a.CheckInTime.HasValue && a.CheckOutTime.HasValue)
                {
                    var duration = (a.CheckOutTime.Value - a.CheckInTime.Value).TotalHours;
                    return duration > 0 ? Math.Round(duration, 2) : 0;
                }
                return a.HoursWorked;
            });

            return Ok(totalHours);
        }

        [HttpGet("history/{year?}")]
        public async Task<ActionResult<List<HseqSafeHourRecord>>> GetPerformanceHistory(int? year = null)
        {
            var targetYear = year ?? DateTime.Now.Year;
            
            // 1. Get Monthly Hours
            var records = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => a.Date.Year == targetYear)
                .Select(a => new { a.Date.Month, a.CheckInTime, a.CheckOutTime, a.HoursWorked })
                .ToListAsync();

            var monthlyHours = records
                .GroupBy(a => a.Month)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => 
                    {
                        if (a.CheckInTime.HasValue && a.CheckOutTime.HasValue)
                        {
                            var duration = (a.CheckOutTime.Value - a.CheckInTime.Value).TotalHours;
                            return duration > 0 ? Math.Round(duration, 2) : 0;
                        }
                        return a.HoursWorked;
                    })
                );

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
                        double lunchHours = 0;
                        var dow = record.Date.DayOfWeek;
                        bool isWeekend = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
                        bool isHoliday = OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(record.Date);
                        
                        if (!isWeekend)
                        {
                            if (record.CheckOutTime.Value.TimeOfDay >= new TimeSpan(13, 0, 0))
                            {
                                lunchHours = 1.0;
                            }
                        }
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

        [HttpGet("project-dashboard/{projectId}")]
        public async Task<ActionResult<object>> GetProjectDashboardStats(Guid projectId)
        {
            var lastIncidentDate = await _context.Incidents
                .Where(i => i.ProjectId == projectId && 
                           (i.Severity == OCC.Shared.Enums.IncidentSeverity.High || 
                            i.Severity == OCC.Shared.Enums.IncidentSeverity.Critical || 
                            i.Severity == OCC.Shared.Enums.IncidentSeverity.Fatality))
                .OrderByDescending(i => i.Date)
                .Select(i => (DateTime?)i.Date)
                .FirstOrDefaultAsync();

            double totalHours = 0;
            var recordsQuery = _context.AttendanceRecords
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId &&
                           (a.Status == OCC.Shared.Models.AttendanceStatus.Present || 
                            a.Status == OCC.Shared.Models.AttendanceStatus.Late || 
                            a.Status == OCC.Shared.Models.AttendanceStatus.LeaveEarly));

            if (lastIncidentDate.HasValue)
            {
                recordsQuery = recordsQuery.Where(a => a.Date > lastIncidentDate.Value);
            }

            var records = await recordsQuery
                .Select(a => new { a.HoursWorked, a.CheckInTime, a.CheckOutTime })
                .ToListAsync();

            totalHours = records.Sum(a => 
            {
                if (a.CheckInTime.HasValue && a.CheckOutTime.HasValue)
                {
                    var duration = (a.CheckOutTime.Value - a.CheckInTime.Value).TotalHours;
                    return duration > 0 ? Math.Round(duration, 2) : 0;
                }
                return a.HoursWorked;
            });

            var incidents = await _context.Incidents
                .Where(i => i.ProjectId == projectId)
                .ToListAsync();

            var incidentsCount = incidents.Count;
            var nearMisses = incidents.Count(i => i.Type == Shared.Enums.IncidentType.NearMiss);
            var injuries = incidents.Count(i => i.Type == Shared.Enums.IncidentType.Injury);
            var environmentals = incidents.Count(i => i.Type == Shared.Enums.IncidentType.Environmental);

            var project = await _context.Projects.FindAsync(projectId);
            var auditQuery = _context.HseqAudits.Include(a => a.Sections).AsNoTracking();

            if (project != null && !string.IsNullOrWhiteSpace(project.Name))
            {
                var projName = project.Name;
                auditQuery = auditQuery.Where(a => a.ProjectId == projectId || (a.SiteName != null && a.SiteName.Contains(projName)));
            }
            else
            {
                auditQuery = auditQuery.Where(a => a.ProjectId == projectId);
            }

            var audits = await auditQuery
                .OrderBy(a => a.Date)
                .ToListAsync();

            var auditsCount = audits.Count;
            var averageAuditScore = auditsCount > 0 ? audits.Average(a => a.ActualScore) : 0;

            var recentAuditScores = audits
                .Select(a => new { Date = a.Date.ToString("yyyy-MM-dd"), ActualScore = a.ActualScore })
                .ToList();

            var categoryStats = audits
                .SelectMany(a => a.Sections ?? new List<HseqAuditSection>())
                .Where(s => s.PossibleScore > 0)
                .GroupBy(s => s.Name)
                .Select(g => new
                {
                    CategoryName = g.Key,
                    AveragePercentage = Math.Round((g.Sum(s => s.ActualScore) / g.Sum(s => s.PossibleScore)) * 100m, 2)
                })
                .ToList();

            return Ok(new 
            {
                TotalSafeHours = totalHours, 
                IncidentsTotal = incidentsCount,
                NearMisses = nearMisses,
                Injuries = injuries,
                Environmentals = environmentals,
                AuditsTotal = auditsCount,
                AverageAuditScore = Math.Round(averageAuditScore, 2),
                RecentAuditScores = recentAuditScores,
                CategoryStats = categoryStats
            });
        }
    }
}
