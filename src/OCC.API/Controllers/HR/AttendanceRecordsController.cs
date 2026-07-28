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
    /// API Controller for attendance records management, hour calculations, leave request synchronization, and file notes upload.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceRecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<AttendanceRecordsController> _logger;

        private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".txt", ".doc", ".docx" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// Initializes a new instance of the <see cref="AttendanceRecordsController"/> class.
        /// </summary>
        public AttendanceRecordsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<AttendanceRecordsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves attendance records filtered by an optional date range.
        /// </summary>
        /// <param name="from">Optional starting date filter.</param>
        /// <param name="to">Optional ending date filter.</param>
        /// <returns>A collection of <see cref="AttendanceRecord"/> objects.</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AttendanceRecord>>> GetAttendanceRecords([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                return BadRequest("The 'from' date cannot be greater than the 'to' date.");
            }

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
                return StatusCode(500, "An internal server error occurred while retrieving attendance records.");
            }
        }

        /// <summary>
        /// Gets a specific attendance record by its ID.
        /// </summary>
        /// <param name="id">The attendance record ID.</param>
        /// <returns>The matching <see cref="AttendanceRecord"/>.</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<AttendanceRecord>> GetAttendanceRecord(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid attendance record ID.");
            }

            try
            {
                var record = await _context.AttendanceRecords.FindAsync(id);
                if (record == null) return NotFound("Attendance record not found.");
                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance record {Id}", id);
                return StatusCode(500, "An internal server error occurred while retrieving the attendance record.");
            }
        }

        /// <summary>
        /// Creates a new attendance record, calculates hours worked, and syncs absence leave requests if applicable.
        /// </summary>
        /// <param name="record">The attendance record entity to create.</param>
        /// <returns>The created <see cref="AttendanceRecord"/>.</returns>
        [HttpPost]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor")]
        public async Task<ActionResult<AttendanceRecord>> PostAttendanceRecord([FromBody] AttendanceRecord record)
        {
            if (record == null)
            {
                return BadRequest("Attendance record payload cannot be null.");
            }

            var errorResponse = ValidateAttendanceRecord(record);
            if (errorResponse != null)
                return BadRequest(errorResponse);

            try
            {
                if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
                
                CalculateHoursWorked(record);

                _context.AttendanceRecords.Add(record);
                await _context.SaveChangesAsync();
                
                await SyncLeaveRequestForAbsenceAsync(record);
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Create", record.Id);
                
                return CreatedAtAction(nameof(GetAttendanceRecord), new { id = record.Id }, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance record");
                return StatusCode(500, "An internal server error occurred while creating the attendance record.");
            }
        }

        /// <summary>
        /// Updates an existing attendance record by ID.
        /// </summary>
        /// <param name="id">The route ID.</param>
        /// <param name="record">The updated record entity.</param>
        /// <returns>No content on success.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor")]
        public async Task<IActionResult> PutAttendanceRecord(Guid id, [FromBody] AttendanceRecord record)
        {
            if (record == null || id != record.Id || id == Guid.Empty)
            {
                return BadRequest("Attendance record ID mismatch or invalid payload.");
            }

            var errorResponse = ValidateAttendanceRecord(record);
            if (errorResponse != null)
                return BadRequest(errorResponse);

            var existingRecord = await _context.AttendanceRecords.FindAsync(id);
            if (existingRecord == null)
            {
                return NotFound("Attendance record not found.");
            }

            _context.Entry(existingRecord).CurrentValues.SetValues(record);
            CalculateHoursWorked(existingRecord);

            try
            {
                await _context.SaveChangesAsync();
                
                await SyncLeaveRequestForAbsenceAsync(existingRecord);
                
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "AttendanceRecord", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AttendanceRecordExists(id)) return NotFound("Attendance record no longer exists.");
                return Conflict("Another user has modified this record. Please reload.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendance record {Id}", id);
                return StatusCode(500, "An internal server error occurred while updating the attendance record.");
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes an attendance record and removes auto-generated leave requests if applicable.
        /// </summary>
        /// <param name="id">The attendance record ID to delete.</param>
        /// <returns>No content on success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Office")]
        public async Task<IActionResult> DeleteAttendanceRecord(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid attendance record ID.");
            }

            try
            {
                var record = await _context.AttendanceRecords.FindAsync(id);
                if (record == null) return NotFound("Attendance record not found.");
                
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

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance record {Id}", id);
                return StatusCode(500, "An internal server error occurred while deleting the attendance record.");
            }
        }

        /// <summary>
        /// Uploads an attachment file note for an attendance record with secure path resolution and extension validation.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        /// <returns>The relative URL path of the uploaded file.</returns>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin, Office, Manager, Supervisor")]
        public async Task<ActionResult<string>> UploadNote(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded or file is empty.");

            if (file.Length > MaxFileSizeBytes)
                return BadRequest("File size exceeds the 10 MB limit.");

            var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExt) || !AllowedExtensions.Contains(fileExt))
            {
                return BadRequest("Invalid file type. Allowed formats: .pdf, .png, .jpg, .jpeg, .txt, .doc, .docx");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "notes");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var safeFileName = $"{Guid.NewGuid()}{fileExt}";
            var filePath = Path.Combine(uploadsFolder, safeFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok($"/uploads/notes/{safeFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading note attachment.");
                return StatusCode(500, "An error occurred while uploading the file.");
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

        private static void CalculateHoursWorked(AttendanceRecord record)
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

            if (record.CheckInTime.HasValue && record.CheckInTime.Value > now.AddMinutes(1))
                return "Clock-in time cannot be in the future.";

            if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue)
            {
                if (record.CheckOutTime.Value < record.CheckInTime.Value)
                    return "Clock-out time cannot be before clock-in time.";
            }

            var overlappingRecords = _context.AttendanceRecords
                .Where(r => r.EmployeeId == record.EmployeeId && r.Id != record.Id && r.Date.Date == record.Date.Date)
                .ToList();

            foreach (var other in overlappingRecords)
            {
                if (record.CheckOutTime == null && other.CheckOutTime == null)
                    return "Employee already has an open shift.";

                DateTime thisIn = record.CheckInTime ?? record.Date.Date;
                DateTime thisOut = record.CheckOutTime ?? DateTime.MaxValue;

                DateTime otherIn = other.CheckInTime ?? other.Date.Date;
                DateTime otherOut = other.CheckOutTime ?? DateTime.MaxValue;

                if (thisIn < otherOut && thisOut > otherIn)
                {
                    return $"Shift overlaps with another recorded shift (In: {otherIn:HH:mm}, Out: {(other.CheckOutTime.HasValue ? otherOut.ToString("HH:mm") : "Open")}).";
                }
            }

            return null;
        }
    }
}
