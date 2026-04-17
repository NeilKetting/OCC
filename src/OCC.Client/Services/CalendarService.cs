using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OCC.Client.Features.CalendarHub.Models;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services
{
    public class CalendarService : ICalendarService
    {
        private readonly IRepository<ProjectTask> _taskRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly IHolidayService _holidayService;
        private readonly ILeaveService _leaveService;
        private readonly IProjectManager _projectManager;
        private readonly IOrderService _orderService;

        public CalendarService(
            IRepository<ProjectTask> taskRepository,
            IRepository<Project> projectRepository,
            IRepository<Employee> employeeRepository,
            IHolidayService holidayService,
            ILeaveService leaveService,
            IProjectManager projectManager,
            IOrderService orderService)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _employeeRepository = employeeRepository;
            _holidayService = holidayService;
            _leaveService = leaveService;
            _projectManager = projectManager;
            _orderService = orderService;
        }

        public async Task<List<CalendarEvent>> GetEventsAsync(DateTime start, DateTime end)
        {
            var events = new List<CalendarEvent>();

            // 1. Fetch Tasks
            var tasks = await _taskRepository.GetAllAsync();
            events.AddRange(tasks
                .Where(t => t.StartDate.Date <= end.Date && t.FinishDate.Date >= start.Date)
                .Select(t => new CalendarEvent
                {
                    Id = t.Id,
                    Type = CalendarEventType.Task,
                    Title = t.Name,
                    Description = t.Description ?? string.Empty,
                    StartDate = t.StartDate,
                    EndDate = t.FinishDate,
                    Color = GetTaskColor(t.Id),
                    IsCompleted = t.ActualCompleteDate.HasValue,
                    OriginalSource = t
                }));

            // 2. Fetch Holidays
            var holidaysResult = await _holidayService.GetHolidaysForYearAsync(start.Year);
            var holidays = holidaysResult.ToList();

            if (start.Year != end.Year)
            {
                var nextYearHolidays = await _holidayService.GetHolidaysForYearAsync(end.Year);
                holidays.AddRange(nextYearHolidays);
            }
            events.AddRange(holidays
                .Where(h => h.Date.Date >= start.Date && h.Date.Date <= end.Date)
                .Select(h => new CalendarEvent
                {
                    Id = Guid.NewGuid(),
                    Type = CalendarEventType.PublicHoliday,
                    Title = h.Name,
                    StartDate = h.Date,
                    EndDate = h.Date,
                    Color = "#FEF2F2", // Light Red
                    OriginalSource = h
                }));

            // 3. Fetch Birthdays (from active employees)
            var employees = await _employeeRepository.GetAllAsync();
            var activeEmployees = employees.Where(e => e.Status == EmployeeStatus.Active).ToList();
            
            // Generate birthday events for the visible range
            for (var dt = start.Date; dt <= end.Date; dt = dt.AddDays(1))
            {
                var birthdayBoys = activeEmployees.Where(e => e.DoB.Month == dt.Month && e.DoB.Day == dt.Day);
                foreach (var emp in birthdayBoys)
                {
                    events.Add(new CalendarEvent
                    {
                        Id = Guid.NewGuid(),
                        Type = CalendarEventType.Birthday,
                        Title = $"{emp.DisplayName}'s Birthday 🎂",
                        StartDate = dt,
                        EndDate = dt,
                        Color = "#FDF2F8", // Pink
                        OriginalSource = emp
                    });
                }
            }

            // 4. Fetch Leave Requests
            foreach (var emp in activeEmployees)
            {
                var leaveRequests = await _leaveService.GetEmployeeRequestsAsync(emp.Id);
                events.AddRange(leaveRequests
                    .Where(r => r.Status == LeaveStatus.Approved && r.EndDate.Date >= start.Date && r.StartDate.Date <= end.Date)
                    .Select(r => new CalendarEvent
                    {
                        Id = r.Id,
                        Type = CalendarEventType.Leave,
                        Title = $"{emp.DisplayName} - {r.LeaveType}",
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        Color = "#10B981", // Green
                        OriginalSource = r
                    }));
            }

            // 5. Fetch Orders
            var orders = await _orderService.GetOrdersAsync();
            events.AddRange(orders
                .Where(o => o.ExpectedDeliveryDate.HasValue && o.ExpectedDeliveryDate.Value.Date >= start.Date && o.ExpectedDeliveryDate.Value.Date <= end.Date)
                .Select(o => new CalendarEvent
                {
                    Id = o.Id,
                    Type = CalendarEventType.OrderDelivery,
                    Title = $"📦 Delivery: {o.OrderNumber} ({o.SupplierName})",
                    Description = $"Status: {o.Status}\nProject: {o.ProjectName}",
                    StartDate = o.ExpectedDeliveryDate!.Value,
                    EndDate = o.ExpectedDeliveryDate!.Value,
                    Color = "#F59E0B", // Amber
                    OriginalSource = o
                }));

            return events;
        }

        private string GetTaskColor(Guid taskId)
        {
            string[] palette = { "#3B82F6", "#10B981", "#8B5CF6", "#F59E0B", "#F43F5E", "#06B6D4", "#6366F1", "#14B8A6" };
            int index = Math.Abs(taskId.GetHashCode()) % palette.Length;
            return palette[index];
        }
    }
}
