using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for computing and serving HSEQ performance statistics, safe man-hours, and safety dashboards.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HseqStatsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HseqStatsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HseqStatsController"/> class.
        /// </summary>
        /// <param name="context">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public HseqStatsController(AppDbContext context, ILogger<HseqStatsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves company-wide HSEQ dashboard metrics including total safe hours, incident counts, and recent audit scores.
        /// </summary>
        /// <returns>Dashboard statistics payload object.</returns>
        [HttpGet("dashboard")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> GetDashboardStats()
        {
            try
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
                    .ToListAsync();

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing company-wide HSEQ dashboard stats.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while computing dashboard statistics.");
            }
        }

        /// <summary>
        /// Retrieves total safe man-hours accumulated for a specific project.
        /// </summary>
        /// <param name="projectId">The project GUID.</param>
        /// <returns>Total safe hours as a double.</returns>
        [HttpGet("project/{projectId}")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<double>> GetProjectSafeHours(Guid projectId)
        {
            if (projectId == Guid.Empty) return BadRequest("Invalid project ID.");

            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing project safe hours for project {ProjectId}.", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while computing project safe hours.");
            }
        }

        /// <summary>
        /// Retrieves monthly safety performance history for a given year (defaults to current year).
        /// </summary>
        /// <param name="year">Optional four-digit year filter.</param>
        /// <returns>List of safe hour performance records.</returns>
        [HttpGet("history/{year?}")]
        [ProducesResponseType(typeof(List<HseqSafeHourRecord>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<HseqSafeHourRecord>>> GetPerformanceHistory(int? year = null)
        {
            if (year.HasValue && (year.Value < 2000 || year.Value > 2100))
            {
                return BadRequest("Invalid year specified.");
            }

            try
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

                // 3. Build Record for each month
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
                        Id = Guid.NewGuid(),
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating HSEQ performance history for year {Year}.", year);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating performance history.");
            }
        }

        /// <summary>
        /// Recalculates hours worked across attendance records based on check-in/out timestamps and lunch deductions.
        /// </summary>
        /// <returns>Status message and number of updated records.</returns>
        [HttpPost("recalculate-hours")]
        [Authorize(Roles = "Admin, Office, Manager, SafetyManager, HseqOfficer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RecalculateHours()
        {
            try
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
                            
                            if (!isWeekend && !isHoliday)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating attendance hours.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while recalculating attendance hours.");
            }
        }

        /// <summary>
        /// Retrieves project-specific HSEQ dashboard statistics including safe hours, incident breakdown, audit average, and category statistics.
        /// </summary>
        /// <param name="projectId">The project GUID.</param>
        /// <returns>Project HSEQ dashboard statistics object.</returns>
        [HttpGet("project-dashboard/{projectId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> GetProjectDashboardStats(Guid projectId)
        {
            if (projectId == Guid.Empty) return BadRequest("Invalid project ID.");

            try
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

                var audits = await _context.HseqAudits
                    .Where(a => a.ProjectId == projectId)
                    .Include(a => a.Sections)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing project dashboard stats for project {ProjectId}.", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while computing project dashboard statistics.");
            }
        }
    }
}

