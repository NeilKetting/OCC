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
                    await RemoveAttendanceRecordsForLeaveAsync(existingRequest.Id);
                    await GenerateAttendanceRecordsForLeaveAsync(existingRequest);
                }
                else if (oldStatus == LeaveStatus.Approved && existingRequest.Status != LeaveStatus.Approved)
                {
                    await RemoveAttendanceRecordsForLeaveAsync(existingRequest.Id);
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

            if (request.Status == LeaveStatus.Approved)
            {
                await RemoveAttendanceRecordsForLeaveAsync(request.Id);
            }

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
            var statusMap = request.LeaveType switch
            {
                LeaveType.Annual => AttendanceStatus.LeaveAuthorized,
                LeaveType.Sick => AttendanceStatus.Sick,
                LeaveType.Unpaid => AttendanceStatus.UnpaidSick,
                LeaveType.AbsentWithoutLeave => AttendanceStatus.Absent,
                _ => AttendanceStatus.LeaveAuthorized
            };

            for (var day = request.StartDate.Date; day <= request.EndDate.Date; day = day.AddDays(1))
            {
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
                if (holidayDates.Contains(day)) continue;

                var existing = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Date.Date == day);

                if (existing != null)
                {
                    if (existing.IsAutoClockIn || existing.Status == AttendanceStatus.Absent)
                    {
                        existing.Status = statusMap;
                        existing.CheckInTime = null;
                        existing.CheckOutTime = null;
                        existing.HoursWorked = 0;
                        existing.Notes = $"Approved Leave: {request.LeaveType}. {notesToken}";
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
                        Notes = $"Approved Leave: {request.LeaveType}. {notesToken}",
                        IsAutoClockIn = true
                    };
                    _context.AttendanceRecords.Add(record);
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task RemoveAttendanceRecordsForLeaveAsync(Guid requestId)
        {
            var notesToken = $"[LeaveRequest:{requestId}]";
            var recordsToRemove = await _context.AttendanceRecords
                .Where(a => a.Notes != null && a.Notes.Contains(notesToken))
                .ToListAsync();

            if (recordsToRemove.Any())
            {
                _context.AttendanceRecords.RemoveRange(recordsToRemove);
                await _context.SaveChangesAsync();
            }
        }
    }
}
