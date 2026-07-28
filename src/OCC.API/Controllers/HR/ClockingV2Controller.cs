using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller for Clocking V2 immutable event logging, dual-write syncing with legacy attendance and daily timesheets.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClockingV2Controller : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ClockingV2Controller> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClockingV2Controller"/> class.
        /// </summary>
        public ClockingV2Controller(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<ClockingV2Controller> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes a clock-in event for an employee, creating V2 events, daily timesheet records, and dual-writing legacy V1 attendance.
        /// </summary>
        /// <param name="request">The clocking event request containing employee ID, timestamp, and source.</param>
        /// <returns>The created <see cref="ClockingEvent"/>.</returns>
        [HttpPost("clock-in")]
        public async Task<IActionResult> ClockIn([FromBody] ClockingEventRequest request)
        {
            if (request == null || request.EmployeeId == Guid.Empty)
                return BadRequest("Invalid clock-in request payload or empty EmployeeId.");

            var sourceStr = string.IsNullOrWhiteSpace(request.Source) ? "WebPortal" : request.Source.Trim();
            var now = DateTime.Now;

            // 1. Create the immutable V2 Clocking Event
            var clockingEvent = new ClockingEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = request.EmployeeId,
                Timestamp = request.Timestamp ?? now,
                EventType = ClockEventType.ClockIn,
                Source = sourceStr
            };

            _context.ClockingEvents.Add(clockingEvent);

            // 2. Dual Write: Create or Update the V2 Daily Timesheet
            var today = clockingEvent.Timestamp.Date;
            var timesheet = await _context.DailyTimesheets
                .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.Date == today);

            if (timesheet == null)
            {
                timesheet = new DailyTimesheet
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = request.EmployeeId,
                    Date = today,
                    FirstInTime = clockingEvent.Timestamp,
                    Status = TimesheetStatus.Present,
                    CalculatedHours = 0m,
                    WageEstimated = 0m
                };
                _context.DailyTimesheets.Add(timesheet);
            }
            else if (timesheet.FirstInTime == null)
            {
                 timesheet.FirstInTime = clockingEvent.Timestamp;
                 timesheet.Status = TimesheetStatus.Present;
            }

            // 3. Dual Write: Create the V1 AttendanceRecord (Legacy compatibility)
            var openLegacyRecord = await _context.AttendanceRecords
                .FirstOrDefaultAsync(r => r.EmployeeId == request.EmployeeId && r.Date.Date == today && r.CheckOutTime == null);

            if (openLegacyRecord == null)
            {
                var employee = await _context.Employees.FindAsync(request.EmployeeId);
                var legacyRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = request.EmployeeId,
                    Date = today,
                    CheckInTime = clockingEvent.Timestamp,
                    ClockInTime = clockingEvent.Timestamp.TimeOfDay,
                    Status = AttendanceStatus.Present,
                    Branch = employee?.Branch ?? "Unknown",
                    CachedHourlyRate = (decimal?)(employee?.HourlyRate ?? 0)
                };
                _context.AttendanceRecords.Add(legacyRecord);
            }

            try
            {
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ClockingEvent", "Create", clockingEvent.Id);
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Create", Guid.Empty); 

                return Ok(clockingEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing V2 clock in for Employee {EmployeeId}", request.EmployeeId);
                return StatusCode(500, "An internal server error occurred while processing clock-in.");
            }
        }

        /// <summary>
        /// Processes a clock-out event for an employee, updating daily timesheet calculations and closing open legacy attendance records.
        /// </summary>
        /// <param name="request">The clocking event request containing employee ID, timestamp, and source.</param>
        /// <returns>The created <see cref="ClockingEvent"/>.</returns>
        [HttpPost("clock-out")]
        public async Task<IActionResult> ClockOut([FromBody] ClockingEventRequest request)
        {
            if (request == null || request.EmployeeId == Guid.Empty)
                return BadRequest("Invalid clock-out request payload or empty EmployeeId.");

            var sourceStr = string.IsNullOrWhiteSpace(request.Source) ? "WebPortal" : request.Source.Trim();
            var now = DateTime.Now;

            // 1. Create the immutable V2 Clocking Event
            var clockingEvent = new ClockingEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = request.EmployeeId,
                Timestamp = request.Timestamp ?? now,
                EventType = ClockEventType.ClockOut,
                Source = sourceStr
            };

            _context.ClockingEvents.Add(clockingEvent);

            // 2. Dual Write: Update the V2 Daily Timesheet
            var today = clockingEvent.Timestamp.Date;
            var timesheet = await _context.DailyTimesheets
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.Date <= today && t.LastOutTime == null);

            if (timesheet != null)
            {
                timesheet.LastOutTime = clockingEvent.Timestamp;
                
                if (timesheet.FirstInTime.HasValue)
                {
                     var hours = (decimal)(timesheet.LastOutTime.Value - timesheet.FirstInTime.Value).TotalHours;
                     if (hours > 5m) hours -= 0.75m; // Deduct lunch if working full day
                     timesheet.CalculatedHours = Math.Max(0m, Math.Round(hours, 2));
                     
                     var employee = await _context.Employees.FindAsync(request.EmployeeId);
                     if (employee != null)
                     {
                         timesheet.WageEstimated = timesheet.CalculatedHours * (decimal)employee.HourlyRate;
                     }
                }
            }

            // 3. Dual Write: Update the V1 AttendanceRecord (Legacy compatibility)
            var openLegacyRecord = await _context.AttendanceRecords
                .OrderByDescending(r => r.CheckInTime)
                .FirstOrDefaultAsync(r => r.EmployeeId == request.EmployeeId && r.CheckOutTime == null);

            if (openLegacyRecord != null)
            {
                openLegacyRecord.CheckOutTime = clockingEvent.Timestamp;
                
                if (openLegacyRecord.CheckInTime.HasValue)
                {
                    var hours = (openLegacyRecord.CheckOutTime.Value - openLegacyRecord.CheckInTime.Value).TotalHours;
                    if (hours > 5) hours -= 0.75;
                    openLegacyRecord.HoursWorked = Math.Max(0, Math.Round(hours, 2));
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ClockingEvent", "Create", clockingEvent.Id);
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Update", openLegacyRecord?.Id ?? Guid.Empty);

                return Ok(clockingEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing V2 clock out for Employee {EmployeeId}", request.EmployeeId);
                return StatusCode(500, "An internal server error occurred while processing clock-out.");
            }
        }

        /// <summary>
        /// Audits and repairs state mismatches between V1 legacy records and V2 clocking sessions. Restricted to Admin and Office roles.
        /// </summary>
        /// <returns>A summary of repaired records count.</returns>
        [HttpPost("repair-sync-v2")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> RepairSyncV2()
        {
            try
            {
                var today = DateTime.Today;
                var latestEvents = await _context.ClockingEvents
                    .GroupBy(e => e.EmployeeId)
                    .Select(g => g.OrderByDescending(e => e.Timestamp).FirstOrDefault())
                    .ToListAsync();

                int repairedCount = 0;

                // 1. Close stale V2 sessions
                foreach (var v2Event in latestEvents.Where(e => e != null && e.EventType == ClockEventType.ClockIn))
                {
                    var currentEvent = v2Event!;

                    var legacyClosed = await _context.AttendanceRecords
                        .AnyAsync(r => r.EmployeeId == currentEvent.EmployeeId && r.Date.Date == today && r.CheckOutTime != null);
                    
                    var timesheetClosed = await _context.DailyTimesheets
                        .AnyAsync(t => t.EmployeeId == currentEvent.EmployeeId && t.Date == today && t.LastOutTime != null);

                    if (legacyClosed || timesheetClosed)
                    {
                        var outTime = DateTime.Now;
                        var clockingEvent = new ClockingEvent
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = currentEvent.EmployeeId,
                            Timestamp = outTime,
                            EventType = ClockEventType.ClockOut,
                            Source = "RepairTool"
                        };
                        _context.ClockingEvents.Add(clockingEvent);
                        repairedCount++;
                    }
                }

                // 2. Open missing V2 sessions
                var activeLegacy = await _context.AttendanceRecords
                    .Where(r => r.Date.Date == today && r.CheckOutTime == null && r.EmployeeId != null)
                    .ToListAsync();

                foreach (var legacy in activeLegacy)
                {
                    var latest = latestEvents.FirstOrDefault(e => e?.EmployeeId == legacy.EmployeeId);
                    if (latest == null || latest.EventType == ClockEventType.ClockOut)
                    {
                        var clockingEvent = new ClockingEvent
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = legacy.EmployeeId ?? Guid.Empty,
                            Timestamp = legacy.CheckInTime ?? DateTime.Now,
                            EventType = ClockEventType.ClockIn,
                            Source = "RepairTool"
                        };
                        _context.ClockingEvents.Add(clockingEvent);
                        repairedCount++;
                    }
                }

                if (repairedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "ClockingEvent", "Create", Guid.Empty);
                }

                return Ok(new { Message = $"Sync complete. Repaired {repairedCount} records.", Count = repairedCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error repairing V2 sync.");
                return StatusCode(500, "An internal server error occurred while executing V2 sync repair.");
            }
        }

        /// <summary>
        /// Gets active clock-in physical presence events across all employees.
        /// </summary>
        /// <returns>A collection of active <see cref="ClockingEvent"/> items.</returns>
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<ClockingEvent>>> GetActivePhysicalPresence()
        {
            try
            {
                var latestEvents = await _context.ClockingEvents
                    .GroupBy(e => e.EmployeeId)
                    .Select(g => g.OrderByDescending(e => e.Timestamp).FirstOrDefault())
                    .ToListAsync();
                    
                var activeEvents = latestEvents
                    .Where(e => e != null && e.EventType == ClockEventType.ClockIn)
                    .Cast<ClockingEvent>()
                    .ToList();
                    
                return Ok(activeEvents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active presence.");
                return StatusCode(500, "An internal server error occurred while retrieving active presence.");
            }
        }

        /// <summary>
        /// Gets daily timesheets for a specific date.
        /// </summary>
        /// <param name="date">The date for timesheets lookup.</param>
        /// <returns>A list of <see cref="DailyTimesheet"/> objects.</returns>
        [HttpGet("timesheets")]
        public async Task<ActionResult<IEnumerable<DailyTimesheet>>> GetDailyTimesheets([FromQuery] DateTime date)
        {
            try
            {
                var timesheets = await _context.DailyTimesheets
                    .Where(t => t.Date == date.Date)
                    .ToListAsync();

                return Ok(timesheets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily timesheets.");
                return StatusCode(500, "An internal server error occurred while retrieving daily timesheets.");
            }
        }

        /// <summary>
        /// Gets daily timesheets within a specific start and end date range.
        /// </summary>
        /// <param name="start">The start date.</param>
        /// <param name="end">The end date.</param>
        /// <returns>A list of <see cref="DailyTimesheet"/> objects.</returns>
        [HttpGet("timesheets/range")]
        public async Task<ActionResult<IEnumerable<DailyTimesheet>>> GetTimesheetsByRange([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            if (start > end)
            {
                return BadRequest("Start date cannot be after end date.");
            }

            try
            {
                var timesheets = await _context.DailyTimesheets
                    .Where(t => t.Date >= start.Date && t.Date <= end.Date)
                    .ToListAsync();

                return Ok(timesheets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving timesheets by range.");
                return StatusCode(500, "An internal server error occurred while retrieving timesheets by range.");
            }
        }
    }

    /// <summary>
    /// Request DTO for clock-in and clock-out operations.
    /// </summary>
    public class ClockingEventRequest
    {
        /// <summary>
        /// The employee unique identifier.
        /// </summary>
        public Guid EmployeeId { get; set; }

        /// <summary>
        /// Optional explicit event timestamp. Defaults to server time if null.
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Source device or client application initiating the clocking event.
        /// </summary>
        public string? Source { get; set; }
    }
}
