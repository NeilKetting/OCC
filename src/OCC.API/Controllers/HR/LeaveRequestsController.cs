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
    public class LeaveRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<LeaveRequestsController> _logger;

        public LeaveRequestsController(AppDbContext context, IHubContext<NotificationHub> hubContext, ILogger<LeaveRequestsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET: api/LeaveRequests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeaveRequest>>> GetLeaveRequests()
        {
            return await _context.LeaveRequests
                .Include(r => r.Employee)
                .AsNoTracking()
                .ToListAsync();
        }

        // GET: api/LeaveRequests/5
        [HttpGet("{id}")]
        public async Task<ActionResult<LeaveRequest>> GetLeaveRequest(Guid id)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();
            return request;
        }

        // POST: api/LeaveRequests
        [HttpPost]
        public async Task<ActionResult<LeaveRequest>> PostLeaveRequest(LeaveRequest request)
        {
            if (request.LeaveType == LeaveType.Unpaid || request.LeaveType == LeaveType.AbsentWithoutLeave)
            {
                request.IsUnpaid = true;
            }
            if (request.Id == Guid.Empty) request.Id = Guid.NewGuid();
            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();
            
            if (request.Status == LeaveStatus.Approved)
            {
                await GenerateAttendanceRecordsForLeaveAsync(request);
            }

            await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Create", request.Id);
            // Notify Admins
            string employeeName = request.Employee != null ? $"{request.Employee.FirstName} {request.Employee.LastName}" : "Unknown Employee";
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New Leave Request from {employeeName}");

            return CreatedAtAction("GetLeaveRequest", new { id = request.Id }, request);
        }

        // PUT: api/LeaveRequests/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutLeaveRequest(Guid id, LeaveRequest request)
        {
            if (id != request.Id) return BadRequest();

            var existingRequest = await _context.LeaveRequests.FindAsync(id);
            if (existingRequest == null)
            {
                return NotFound();
            }

            var oldStatus = existingRequest.Status;

            _context.Entry(existingRequest).CurrentValues.SetValues(request);
            if (existingRequest.LeaveType == LeaveType.Unpaid || existingRequest.LeaveType == LeaveType.AbsentWithoutLeave)
            {
                existingRequest.IsUnpaid = true;
            }

            try
            {
                await _context.SaveChangesAsync();

                if (existingRequest.Status == LeaveStatus.Approved)
                {
                    await RemoveAttendanceRecordsForLeaveAsync(existingRequest);
                    await GenerateAttendanceRecordsForLeaveAsync(existingRequest);
                }
                else if (oldStatus == LeaveStatus.Approved && existingRequest.Status != LeaveStatus.Approved)
                {
                    await RemoveAttendanceRecordsForLeaveAsync(existingRequest);
                }

                await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Update", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LeaveRequestExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/LeaveRequests/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLeaveRequest(Guid id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null) return NotFound();

            await RemoveAttendanceRecordsForLeaveAsync(request);

            _context.LeaveRequests.Remove(request);
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "LeaveRequest", "Delete", id);

            return NoContent();
        }

        private bool LeaveRequestExists(Guid id) => _context.LeaveRequests.Any(e => e.Id == id);

        private async Task GenerateAttendanceRecordsForLeaveAsync(LeaveRequest request)
        {
            var employee = await _context.Employees.FindAsync(request.EmployeeId);
            if (employee == null) return;

            var holidays = await _context.PublicHolidays.Select(h => h.Date.Date).ToListAsync();
            var holidayDates = new HashSet<DateTime>(holidays);

            var notesToken = $"[LeaveRequest:{request.Id}]";

            // Safety split check in case client didn't calculate or save PaidDays/UnpaidDays
            if (request.PaidDays == 0 && request.UnpaidDays == 0)
            {
                if (request.LeaveType == LeaveType.CulturalObligations)
                {
                    double totalDays = request.NumberOfDays;
                    double cappedPaid = Math.Min(3.0, totalDays);
                    double employeeAnnualBalance = employee.AnnualLeaveBalance;
                    
                    request.PaidDays = Math.Max(0, Math.Min(cappedPaid, employeeAnnualBalance));
                    request.UnpaidDays = Math.Max(0, totalDays - request.PaidDays);
                }
                else if (request.LeaveType == LeaveType.Unpaid || request.LeaveType == LeaveType.AbsentWithoutLeave)
                {
                    request.PaidDays = 0;
                    request.UnpaidDays = request.NumberOfDays;
                    request.IsUnpaid = true;
                }
                else
                {
                    request.PaidDays = request.NumberOfDays;
                    request.UnpaidDays = 0;
                }
            }
            
            // Standard shift hours calculation (as confirmed by client/wage runs)
            double dailyHours;
            if (employee.ShiftStartTime.HasValue && employee.ShiftEndTime.HasValue)
            {
                dailyHours = (employee.ShiftEndTime.Value - employee.ShiftStartTime.Value).TotalHours;
                if (employee.ShiftEndTime.Value.Hours >= 13)
                {
                    dailyHours -= 1.0;
                }
                if (dailyHours < 0) dailyHours = 0;
            }
            else if (string.Equals(employee.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase) || string.Equals(employee.Branch, "CPT", StringComparison.OrdinalIgnoreCase))
            {
                dailyHours = 8.5;
            }
            else
            {
                dailyHours = 8.75;
            }

            // Fractions for allocation
            double totalRequestedDays = request.NumberOfDays > 0 ? request.NumberOfDays : 1.0;
            double paidFraction = request.PaidDays / totalRequestedDays;
            double unpaidFraction = request.UnpaidDays / totalRequestedDays;

            double remainingPaidDays = request.PaidDays;
            double remainingUnpaidDays = request.UnpaidDays;

            for (var day = request.StartDate.Date; day <= request.EndDate.Date; day = day.AddDays(1))
            {
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
                if (holidayDates.Contains(day)) continue;

                double allocatedPaidHours = 0;
                double allocatedUnpaidHours = 0;
                AttendanceStatus statusMap = AttendanceStatus.LeaveAuthorized;

                if (request.DurationType == LeaveDurationType.FullDay)
                {
                    if (remainingPaidDays > 0)
                    {
                        if (remainingPaidDays >= 1.0)
                        {
                            allocatedPaidHours = dailyHours;
                            remainingPaidDays -= 1.0;
                        }
                        else
                        {
                            allocatedPaidHours = remainingPaidDays * dailyHours;
                            allocatedUnpaidHours = (1.0 - remainingPaidDays) * dailyHours;
                            remainingPaidDays = 0;
                        }
                        statusMap = request.LeaveType == LeaveType.Sick ? AttendanceStatus.Sick : AttendanceStatus.LeaveAuthorized;
                    }
                    else if (remainingUnpaidDays > 0)
                    {
                        if (remainingUnpaidDays >= 1.0)
                        {
                            allocatedUnpaidHours = dailyHours;
                            remainingUnpaidDays -= 1.0;
                        }
                        else
                        {
                            allocatedUnpaidHours = remainingUnpaidDays * dailyHours;
                            remainingUnpaidDays = 0;
                        }
                        statusMap = AttendanceStatus.UnpaidSick;
                    }
                }
                else if (request.DurationType == LeaveDurationType.MorningHalfDay || request.DurationType == LeaveDurationType.AfternoonHalfDay)
                {
                    allocatedPaidHours = paidFraction * 0.5 * dailyHours;
                    allocatedUnpaidHours = unpaidFraction * 0.5 * dailyHours;
                    statusMap = (allocatedPaidHours > 0 && !request.IsUnpaid)
                        ? (request.LeaveType == LeaveType.Sick ? AttendanceStatus.Sick : AttendanceStatus.LeaveAuthorized) 
                        : (request.LeaveType == LeaveType.Sick ? AttendanceStatus.UnpaidSick : AttendanceStatus.UnpaidHalfDay);
                }
                else if (request.DurationType == LeaveDurationType.Hourly)
                {
                    double hrs = request.HoursRequested ?? 0.0;
                    allocatedPaidHours = paidFraction * hrs;
                    allocatedUnpaidHours = unpaidFraction * hrs;
                    statusMap = allocatedPaidHours > 0 
                        ? (request.LeaveType == LeaveType.Sick ? AttendanceStatus.Sick : AttendanceStatus.LeaveAuthorized) 
                        : AttendanceStatus.UnpaidSick;
                }

                var existing = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Date.Date == day);

                if (existing != null)
                {
                    if (request.DurationType == LeaveDurationType.FullDay)
                    {
                        existing.Status = statusMap;
                        existing.CheckInTime = null;
                        existing.CheckOutTime = null;
                        existing.HoursWorked = 0;
                        existing.PaidLeaveHours = allocatedPaidHours;
                        existing.UnpaidLeaveHours = allocatedUnpaidHours;
                        existing.Notes = $"Approved Leave: {request.LeaveType} ({request.DurationType}). {notesToken}";
                        existing.IsAutoClockIn = true;
                        existing.DoctorsNoteImagePath = request.DoctorsNoteImagePath;
                    }
                    else
                    {
                        // Partial day leave should NOT clear clock-in/out times!
                        existing.PaidLeaveHours = allocatedPaidHours;
                        existing.UnpaidLeaveHours = allocatedUnpaidHours;
                        existing.Notes = (existing.Notes ?? "") + $" [Partial Leave: {request.LeaveType} ({request.DurationType}). {notesToken}]";
                        if (!string.IsNullOrEmpty(request.DoctorsNoteImagePath))
                        {
                            existing.DoctorsNoteImagePath = request.DoctorsNoteImagePath;
                        }
                    }
                }
                else
                {
                    var record = new AttendanceRecord
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = request.EmployeeId,
                        Date = day,
                        Status = statusMap,
                        Branch = employee.Branch,
                        Notes = $"Approved Leave: {request.LeaveType} ({request.DurationType}). {notesToken}",
                        IsAutoClockIn = true,
                        PaidLeaveHours = allocatedPaidHours,
                        UnpaidLeaveHours = allocatedUnpaidHours,
                        DoctorsNoteImagePath = request.DoctorsNoteImagePath
                    };
                    _context.AttendanceRecords.Add(record);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task RemoveAttendanceRecordsForLeaveAsync(LeaveRequest request)
        {
            var notesToken = $"[LeaveRequest:{request.Id}]";
            var startDate = request.StartDate.Date;
            var endDate = request.EndDate.Date;

            // Find all records explicitly linked via token OR on the leave date range for this employee with leave status/hours/note
            var recordsToClean = await _context.AttendanceRecords
                .Where(a => a.EmployeeId == request.EmployeeId &&
                            ((a.Notes != null && a.Notes.Contains(notesToken)) ||
                             (a.Date >= startDate && a.Date <= endDate &&
                              (a.Status == AttendanceStatus.Sick || 
                               a.Status == AttendanceStatus.LeaveAuthorized || 
                               a.Status == AttendanceStatus.UnpaidSick || 
                               a.Status == AttendanceStatus.UnpaidLeave || 
                               a.Status == AttendanceStatus.UnpaidHalfDay ||
                               (a.PaidLeaveHours.HasValue && a.PaidLeaveHours > 0) ||
                               (a.UnpaidLeaveHours.HasValue && a.UnpaidLeaveHours > 0) ||
                               !string.IsNullOrEmpty(a.DoctorsNoteImagePath)))))
                .ToListAsync();

            if (!recordsToClean.Any()) return;

            var recordsToRemove = new List<AttendanceRecord>();

            foreach (var record in recordsToClean)
            {
                if (record.CheckInTime == null || record.HoursWorked == 0 || record.IsAutoClockIn)
                {
                    recordsToRemove.Add(record);
                }
                else
                {
                    record.Status = AttendanceStatus.Present;
                    record.PaidLeaveHours = null;
                    record.UnpaidLeaveHours = null;
                    record.DoctorsNoteImagePath = null;
                    if (!string.IsNullOrEmpty(record.Notes))
                    {
                        record.Notes = record.Notes.Replace(notesToken, "").Replace($"Approved Leave: {request.LeaveType} ({request.DurationType}).", "").Trim();
                    }
                }
            }

            if (recordsToRemove.Any())
            {
                _context.AttendanceRecords.RemoveRange(recordsToRemove);
            }

            await _context.SaveChangesAsync();
        }
    }
}
