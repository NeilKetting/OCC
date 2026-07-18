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
    public class ClockingV2Controller : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<ClockingV2Controller> _logger;

        public ClockingV2Controller(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<ClockingV2Controller> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("clock-in")]
        public async Task<IActionResult> ClockIn([FromBody] ClockingEventRequest request)
        {
            if (request == null || request.EmployeeId == Guid.Empty)
                return BadRequest("Invalid clock-in request.");

            var now = DateTime.Now;

            // 1. Create the immutable V2 Clocking Event
            var clockingEvent = new ClockingEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = request.EmployeeId,
                Timestamp = request.Timestamp ?? now,
                EventType = ClockEventType.ClockIn,
                Source = request.Source ?? "WebPortal"
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
                    CalculatedHours = 0,
                    WageEstimated = 0
                };
                _context.DailyTimesheets.Add(timesheet);
            }
            else if (timesheet.FirstInTime == null)
            {
                 timesheet.FirstInTime = clockingEvent.Timestamp;
                 timesheet.Status = TimesheetStatus.Present;
            }

            // 3. Dual Write: Create the V1 AttendanceRecord (Legacy compatibility)
            // We must create an open shift if one doesn't exist to not break the old Live Board
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
                
                // SignalR updates for both systems
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "ClockingEvent", "Create", clockingEvent.Id);
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Create", Guid.Empty); 

                return Ok(clockingEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing V2 clock in for Employee {EmployeeId}", request.EmployeeId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("clock-out")]
        public async Task<IActionResult> ClockOut([FromBody] ClockingEventRequest request)
        {
            if (request == null || request.EmployeeId == Guid.Empty)
                return BadRequest("Invalid clock-out request.");

            var now = DateTime.Now;

            // 1. Create the immutable V2 Clocking Event
            var clockingEvent = new ClockingEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = request.EmployeeId,
                Timestamp = request.Timestamp ?? now,
                EventType = ClockEventType.ClockOut,
                Source = request.Source ?? "WebPortal"
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
                
                // Very basic hours calc - real system might subtract lunch
                if (timesheet.FirstInTime.HasValue)
                {
                     var hours = (decimal)(timesheet.LastOutTime.Value - timesheet.FirstInTime.Value).TotalHours;
                     if (hours > 5) hours -= 0.75m; // Deduct lunch if working full day
                     timesheet.CalculatedHours = Math.Max(0, Math.Round(hours, 2));
                     
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
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpPost("repair-sync-v2")]
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
                    // v2Event is filtered to be non-null above
                    var currentEvent = v2Event!;

                    // Check if Legacy OR V2 Timesheet says they are clocked out for today
                    var legacyClosed = await _context.AttendanceRecords
                        .AnyAsync(r => r.EmployeeId == currentEvent.EmployeeId && r.Date.Date == today && r.CheckOutTime != null);
                    
                    var timesheetClosed = await _context.DailyTimesheets
                        .AnyAsync(t => t.EmployeeId == currentEvent.EmployeeId && t.Date == today && t.LastOutTime != null);

                    if (legacyClosed || timesheetClosed)
                    {
                        // They are closed in source of truth, but V2 event log says ClockIn. Add a synthetic ClockOut.
                        var outTime = DateTime.Now; // Or fetch from the closed record
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

                // 2. Open missing V2 sessions (Legacy says present, V2 says logic/missing)
                var activeLegacy = await _context.AttendanceRecords
                    .Where(r => r.Date.Date == today && r.CheckOutTime == null && r.EmployeeId != null)
                    .ToListAsync();

                foreach (var legacy in activeLegacy)
                {
                    var latest = latestEvents.FirstOrDefault(e => e?.EmployeeId == legacy.EmployeeId);
                    if (latest == null || latest.EventType == ClockEventType.ClockOut)
                    {
                        // Legacy says they are here, but V2 says they aren't. Add a synthetic ClockIn.
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
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<ClockingEvent>>> GetActivePhysicalPresence()
        {
            try
            {
                // We fetch the absolute latest event for EVERY employee.
                // If that latest event is a ClockIn, they are physically present.
                // This correctly handles overnight shifts and legacy data gaps.
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
                return StatusCode(500, "Internal server error");
            }
        }

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
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("timesheets/range")]
        public async Task<ActionResult<IEnumerable<DailyTimesheet>>> GetTimesheetsByRange([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
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
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class ClockingEventRequest
    {
        public Guid EmployeeId { get; set; }
        public DateTime? Timestamp { get; set; }
        public string? Source { get; set; }
    }
}

