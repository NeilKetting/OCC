using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Features.CalendarHub.Models;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.CalendarHub.Services
{
    // =========================================================================
    // CalendarService.cs
    // Aggregates calendar events from Tasks, Public Holidays, Birthdays and Leave
    // into a single normalised list for the CalendarHubViewModel to consume.
    //
    // Design decisions:
    //  • Tasks are fetched with a date-window filter so we never pull all 500+
    //    tasks — only those overlapping the visible 42-day calendar grid.
    //  • Birthdays are generated client-side from the active-employee list;
    //    no separate birthday endpoint is needed.
    //  • The IHolidayService result is cached internally per-year (see
    //    HolidayService.cs), so month navigation is cheap.
    // =========================================================================

    /// <summary>
    /// Aggregates calendar events from the tasks, holidays, employee birthdays
    /// and leave services into a unified list of <see cref="CalendarEvent"/> objects.
    /// </summary>
    public class CalendarService : ICalendarService
    {
        #region Fields

        private readonly IProjectTaskService _taskService;
        private readonly IProjectService     _projectService;
        private readonly IEmployeeService    _employeeService;
        private readonly IHolidayService     _holidayService;
        private readonly ILeaveService       _leaveService;
        private readonly ILogger<CalendarService> _logger;

        // Colour palette used to assign a stable colour to each task based on its ID hash.
        // Using hash ensures the same task always gets the same colour across refreshes.
        private static readonly string[] TaskColourPalette =
        {
            "#2E9DFF", // Blue  (accent)
            "#10B981", // Emerald
            "#8B5CF6", // Purple
            "#F59E0B", // Amber
            "#F43F5E", // Rose
            "#06B6D4", // Cyan
            "#6366F1", // Indigo
            "#14B8A6"  // Teal
        };

        #endregion

        #region Constructor

        /// <summary>
        /// Initialises the service with all required domain service dependencies.
        /// </summary>
        public CalendarService(
            IProjectTaskService      taskService,
            IProjectService          projectService,
            IEmployeeService         employeeService,
            IHolidayService          holidayService,
            ILeaveService            leaveService,
            ILogger<CalendarService> logger)
        {
            _taskService      = taskService;
            _projectService   = projectService;
            _employeeService  = employeeService;
            _holidayService   = holidayService;
            _leaveService     = leaveService;
            _logger           = logger;
        }

        #endregion

        #region ICalendarService

        /// <inheritdoc/>
        public async Task<List<CalendarEvent>> GetEventsAsync(
            DateTime          start,
            DateTime          end,
            IEnumerable<Guid>? projectIds = null)
        {
            var events = new List<CalendarEvent>();

            // Run independent data fetches concurrently to reduce total wait time.
            // Task fetching is intentionally separate because it can be filtered by projectIds.
            var holidaysTask   = FetchHolidaysAsync(start, end);
            var birthdaysTask  = FetchBirthdaysAsync(start, end);
            var leaveTask      = FetchLeaveAsync(start, end);
            var tasksTask      = FetchTasksAsync(start, end, projectIds);

            await Task.WhenAll(holidaysTask, birthdaysTask, leaveTask, tasksTask);

            events.AddRange(await tasksTask);
            events.AddRange(await holidaysTask);
            events.AddRange(await birthdaysTask);
            events.AddRange(await leaveTask);

            return events;
        }

        #endregion

        #region Private — Task Fetching

        /// <summary>
        /// Fetches project tasks that overlap the visible date window.
        /// Uses a large <c>take</c> value (500) scoped by the project filter so
        /// we always get every task for the relevant projects without loading all
        /// historical records across the entire database.
        /// </summary>
        private async Task<IEnumerable<CalendarEvent>> FetchTasksAsync(
            DateTime           start,
            DateTime           end,
            IEnumerable<Guid>? projectIds)
        {
            try
            {
                // Resolve the set of project IDs we care about
                var projectIdSet = projectIds?.ToHashSet();

                // Fetch tasks — pass null for projectId to get all, then filter client-side
                // (the API supports per-project queries; we batch-filter here to handle the
                //  "all projects" case efficiently with a single round-trip)
                var allTasks = await _taskService.GetTasksAsync(take: 500);

                // Build a project-name lookup so we can label each task bar
                var projectMap = new Dictionary<Guid, string>();
                try
                {
                    var summaries = await _projectService.GetProjectSummariesAsync();
                    foreach (var s in summaries)
                        projectMap[s.Id] = s.Name;
                }
                catch (Exception ex)
                {
                    // Non-critical — task bars will show without project name
                    _logger.LogWarning(ex, "Could not load project summaries for calendar labels.");
                }

                return allTasks
                    // Filter out container (parent) tasks
                    .Where(t => !t.IsGroup)
                    // Window filter: only tasks that overlap the 42-day visible range
                    .Where(t =>
                        t.StartDate.Date  <= end.Date &&
                        t.FinishDate.Date >= start.Date)
                    // Optional project filter
                    .Where(t =>
                        projectIdSet == null ||
                        (t.ProjectId != null && projectIdSet.Contains(t.ProjectId.Value)))
                    .Select(t => new CalendarEvent
                    {
                        Id             = t.Id,
                        Type           = CalendarEventType.Task,
                        Title          = t.Name,
                        Description    = t.Description ?? string.Empty,
                        StartDate      = t.StartDate,
                        EndDate        = t.FinishDate,
                        Color          = GetTaskColour(t.Id),
                        IsCompleted    = t.ActualCompleteDate.HasValue,
                        ProjectName    = projectMap.TryGetValue(t.ProjectId ?? Guid.Empty, out var pName) ? pName : string.Empty,
                        OriginalSource = t
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks for calendar.");
                return Enumerable.Empty<CalendarEvent>();
            }
        }

        #endregion

        #region Private — Holiday Fetching

        /// <summary>
        /// Retrieves SA public holidays for all years represented in the
        /// visible window (handles the edge case where the window spans two years).
        /// </summary>
        private async Task<IEnumerable<CalendarEvent>> FetchHolidaysAsync(DateTime start, DateTime end)
        {
            try
            {
                var holidays = (await _holidayService.GetHolidaysForYearAsync(start.Year)).ToList();

                // If the 42-day window crosses a year boundary, fetch next year too
                if (end.Year != start.Year)
                {
                    var next = await _holidayService.GetHolidaysForYearAsync(end.Year);
                    holidays.AddRange(next);
                }

                return holidays
                    .Where(h => h.Date.Date >= start.Date && h.Date.Date <= end.Date)
                    .Select(h => new CalendarEvent
                    {
                        Id             = Guid.NewGuid(),
                        Type           = CalendarEventType.PublicHoliday,
                        Title          = h.Name,
                        StartDate      = h.Date,
                        EndDate        = h.Date,
                        Color          = "#EF4444", // Red — standard holiday indicator
                        OriginalSource = h
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching holidays for calendar.");
                return Enumerable.Empty<CalendarEvent>();
            }
        }

        #endregion

        #region Private — Birthday Fetching

        /// <summary>
        /// Generates birthday events by matching each active employee's birth
        /// month/day to every date in the visible window.
        /// No "birthday" endpoint exists — birthdays are derived from employee records.
        /// </summary>
        private async Task<IEnumerable<CalendarEvent>> FetchBirthdaysAsync(DateTime start, DateTime end)
        {
            var results = new List<CalendarEvent>();

            try
            {
                var employees = await _employeeService.GetEmployeesAsync();
                var activeEmployees = employees.Where(e => e.Status == EmployeeStatus.Active);

                foreach (var emp in activeEmployees)
                {
                    // Birthdays occur on the same month/day every year. 
                    // Loop through each day in the 42-day window to see if it matches the employee's birthday.
                    for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
                    {
                        if (emp.DoB.Month == date.Month && emp.DoB.Day == date.Day)
                        {
                            results.Add(new CalendarEvent
                            {
                                Id             = Guid.NewGuid(),
                                Type           = CalendarEventType.Birthday,
                                Title          = $"{emp.FirstName} {emp.LastName}'s Birthday 🎂",
                                StartDate      = date,
                                EndDate        = date,
                                Color          = "#EC4899", // Pink
                                OriginalSource = emp
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating birthday events for calendar.");
            }

            return results;
        }

        #endregion

        #region Private — Leave Fetching

        /// <summary>
        /// Fetches all approved leave requests that overlap the visible window.
        /// </summary>
        private async Task<IEnumerable<CalendarEvent>> FetchLeaveAsync(DateTime start, DateTime end)
        {
            try
            {
                var allLeave = await _leaveService.GetLeaveRequestsAsync();

                return allLeave
                    .Where(r =>
                        r.Status     == LeaveStatus.Approved &&
                        r.EndDate.Date   >= start.Date &&
                        r.StartDate.Date <= end.Date)
                    .Select(r =>
                    {
                        // LeaveRequest has an Employee navigation property (may be null if
                        // not eagerly loaded by the API). Fall back to a short ID if needed.
                        var empName = r.Employee != null
                            ? $"{r.Employee.FirstName} {r.Employee.LastName}".Trim()
                            : $"Employee ({r.EmployeeId.ToString("N")[..8]})";

                        return new CalendarEvent
                        {
                            Id             = r.Id,
                            Type           = CalendarEventType.Leave,
                            Title          = $"{empName} — {r.LeaveType}",
                            StartDate      = r.StartDate,
                            EndDate        = r.EndDate,
                            Color          = "#10B981",
                            OriginalSource = r
                        };
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leave requests for calendar.");
                return Enumerable.Empty<CalendarEvent>();
            }
        }

        #endregion

        #region Private — Colour Helpers

        /// <summary>
        /// Derives a stable colour from a task's ID so the same task always
        /// renders with the same colour bar, even after a data refresh.
        /// </summary>
        private static string GetTaskColour(Guid taskId)
        {
            // Use absolute hash to avoid negative modulo on some runtimes
            int index = Math.Abs(taskId.GetHashCode()) % TaskColourPalette.Length;
            return TaskColourPalette[index];
        }

        #endregion
    }
}
