using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceRecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<AttendanceRecordsController> _logger;

        public AttendanceRecordsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<AttendanceRecordsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/AttendanceRecords
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AttendanceRecord>>> GetAttendanceRecords([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var query = _context.AttendanceRecords.AsNoTracking();
                if (from.HasValue)
                {
                    var fromDate = from.Value.Date;
                    query = query.Where(r => r.Date >= fromDate);
                }
                if (to.HasValue)
                {
                    var toDate = to.Value.Date;
                    query = query.Where(r => r.Date <= toDate);
                }
                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance records");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/AttendanceRecords/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AttendanceRecord>> GetAttendanceRecord(Guid id)
        {
            try
            {
                var record = await _context.AttendanceRecords.FindAsync(id);
                if (record == null) return NotFound();
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/AttendanceRecords
        [HttpPost]
        public async Task<ActionResult<AttendanceRecord>> PostAttendanceRecord(AttendanceRecord record)
        {
            var errorResponse = ValidateAttendanceRecord(record);
            if (errorResponse != null)
                return BadRequest(errorResponse);

            try
            {
                if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
                
                // Calculate hours before saving
                CalculateHoursWorked(record);

                _context.AttendanceRecords.Add(record);
                await _context.SaveChangesAsync();
                
                await SyncLeaveRequestForAbsenceAsync(record);
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Create", record.Id);
                await _hubContext.Clients.All.SendAsync("AttendanceRecordChanged", new EntityChangeDto<AttendanceRecord> { Action = "Created", Entity = record, EntityId = record.Id });
                
                return CreatedAtAction("GetAttendanceRecord", new { id = record.Id }, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance record");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/AttendanceRecords/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAttendanceRecord(Guid id, AttendanceRecord record)
        {
            if (id != record.Id) return BadRequest();

            var errorResponse = ValidateAttendanceRecord(record);
            if (errorResponse != null)
                return BadRequest(errorResponse);

            var existingRecord = await _context.AttendanceRecords.FindAsync(id);
            if (existingRecord == null)
            {
                return NotFound();
            }

            _context.Entry(existingRecord).CurrentValues.SetValues(record);
            CalculateHoursWorked(existingRecord);

            try
            {
                await _context.SaveChangesAsync();
                
                await SyncLeaveRequestForAbsenceAsync(existingRecord);
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Update", id);
                await _hubContext.Clients.All.SendAsync("AttendanceRecordChanged", new EntityChangeDto<AttendanceRecord> { Action = "Updated", Entity = existingRecord, EntityId = id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AttendanceRecordExists(id)) return NotFound();
                else throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendance record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
            return NoContent();
        }

        // DELETE: api/AttendanceRecords/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttendanceRecord(Guid id)
        {
            try
            {
                var record = await _context.AttendanceRecords.FindAsync(id);
                if (record == null) return NotFound();
                
                var employeeId = record.EmployeeId;
                var date = record.Date.Date;
                var wasAbsent = record.Status == AttendanceStatus.Absent;

                _context.AttendanceRecords.Remove(record);
                await _context.SaveChangesAsync();
                
                if (wasAbsent)
                {
                    var autoLeave = await _context.LeaveRequests
                        .FirstOrDefaultAsync(l => l.EmployeeId == employeeId && 
                                                  l.StartDate == date && 
                                                  l.EndDate == date && 
                                                  l.LeaveType == LeaveType.AbsentWithoutLeave && 
                                                  l.Reason == "UNPAID -Absent without leave");
                    if (autoLeave != null)
                    {
                        _context.LeaveRequests.Remove(autoLeave);
                        await _context.SaveChangesAsync();
                        await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Delete", autoLeave.Id);
                    }
                }
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Delete", id);
                await _hubContext.Clients.All.SendAsync("AttendanceRecordChanged", new EntityChangeDto<AttendanceRecord> { Action = "Deleted", Entity = record, EntityId = id });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance record {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private bool AttendanceRecordExists(Guid id) => _context.AttendanceRecords.Any(e => e.Id == id);

        private async Task SyncLeaveRequestForAbsenceAsync(AttendanceRecord record)
        {
            if (record.EmployeeId == null) return;
            var employeeId = record.EmployeeId.Value;
            var date = record.Date.Date;
            if (record.Status == AttendanceStatus.Absent)
            {
                var exists = await _context.LeaveRequests
                    .AnyAsync(l => l.EmployeeId == employeeId && l.StartDate <= date && l.EndDate >= date);
                if (!exists)
                {
                    var leaveReq = new LeaveRequest
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employeeId,
                        StartDate = date,
                        EndDate = date,
                        NumberOfDays = 1,
                        DurationType = LeaveDurationType.FullDay,
                        PaidDays = 0,
                        UnpaidDays = 1,
                        LeaveType = LeaveType.AbsentWithoutLeave,
                        Status = LeaveStatus.Approved,
                        Reason = "UNPAID -Absent without leave",
                        IsUnpaid = true,
                        CreatedDate = DateTime.UtcNow,
                        ActionedDate = DateTime.UtcNow
                    };
                    _context.LeaveRequests.Add(leaveReq);
                    await _context.SaveChangesAsync();
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Create", leaveReq.Id);
                }
            }
            else
            {
                var autoLeave = await _context.LeaveRequests
                    .FirstOrDefaultAsync(l => l.EmployeeId == employeeId && 
                                              l.StartDate == date && 
                                              l.EndDate == date && 
                                              l.LeaveType == LeaveType.AbsentWithoutLeave && 
                                              l.Reason == "UNPAID -Absent without leave");
                if (autoLeave != null)
                {
                    _context.LeaveRequests.Remove(autoLeave);
                    await _context.SaveChangesAsync();
                    await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Delete", autoLeave.Id);
                }
            }
        }

        [HttpPost("upload")]
        public async Task<ActionResult<string>> UploadNote(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "notes");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path
            return Ok($"/uploads/notes/{uniqueFileName}");
        }

        private void CalculateHoursWorked(AttendanceRecord record)
        {
            if (record.Status == AttendanceStatus.Absent || record.Status == AttendanceStatus.UnpaidSick || record.Status == AttendanceStatus.UnpaidLeave)
            {
                record.HoursWorked = 0;
                return;
            }

            if (record.CheckInTime != null && record.CheckOutTime != null)
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
                        // Unpaid lunch is 1 hour (12:00-13:00). Deduct 1 hour only if checkout is at or after 13:00.
                        if (record.CheckOutTime.Value.TimeOfDay >= new TimeSpan(13, 0, 0))
                        {
                            lunchHours = 1.0;
                        }
                    }
                    record.HoursWorked = Math.Max(0, Math.Round(duration.TotalHours - lunchHours, 2));
                }
                else
                {
                    record.HoursWorked = 0;
                }
            }
            else
            {
                record.HoursWorked = 0;
            }
        }

        private string? ValidateAttendanceRecord(AttendanceRecord record)
        {
            var now = DateTime.Now;

            // 1. Future time checks (Allow 1 minute leniency for server-client desync)
            if (record.CheckInTime.HasValue && record.CheckInTime.Value > now.AddMinutes(1))
                return "Clock-in time cannot be in the future.";
            


            // 2. Order check
            if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue)
            {
                if (record.CheckOutTime.Value < record.CheckInTime.Value)
                    return "Clock-out time cannot be before clock-in time.";
            }

            // 3. Overlap check for the same employee
            var overlappingRecords = _context.AttendanceRecords
                .Where(r => r.EmployeeId == record.EmployeeId && r.Id != record.Id && r.Date.Date == record.Date.Date)
                .ToList();

            foreach (var other in overlappingRecords)
            {
                // 3a. Check for multiple open shifts
                if (record.CheckOutTime == null && other.CheckOutTime == null)
                    return "Employee already has an open shift.";

                // 3b. Temporal overlap
                DateTime thisIn = record.CheckInTime ?? record.Date.Date;
                DateTime thisOut = record.CheckOutTime ?? DateTime.MaxValue;

                DateTime otherIn = other.CheckInTime ?? other.Date.Date;
                DateTime otherOut = other.CheckOutTime ?? DateTime.MaxValue;

                // Simple overlap condition
                if (thisIn < otherOut && thisOut > otherIn)
                {
                    return $"Shift overlaps with another recorded shift (In: {otherIn:HH:mm}, Out: {(other.CheckOutTime.HasValue ? otherOut.ToString("HH:mm") : "Open")}).";
                }
            }

            return null;
        }
    }
}
