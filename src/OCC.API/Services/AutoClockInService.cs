using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System.Text.Json;

namespace OCC.API.Services
{
    public class AutoClockInService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoClockInService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

        public AutoClockInService(IServiceProvider serviceProvider, ILogger<AutoClockInService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoClockInService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessClockInAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing automatic clock-ins.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("AutoClockInService is stopping.");
        }

        private async Task ProcessClockInAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Check if feature is globally enabled
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.Key == "CompanyProfile", stoppingToken);
            if (setting == null || string.IsNullOrEmpty(setting.Value))
            {
                return;
            }

            var companyDetails = JsonSerializer.Deserialize<CompanyDetails>(setting.Value);
            if (companyDetails == null || !companyDetails.AutoClockInEnabled)
            {
                return; // Feature is disabled
            }

            // South Africa Standard Time (SAST, UTC+2) is the local timezone for all branches (JHB and CPT)
            var saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            var saTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saTimeZone);
            var today = saTime.Date;
            var todayDay = saTime.DayOfWeek;
            var currentTime = saTime.TimeOfDay;

            // Get active deployments for today to check if any crew is dispatched
            var deploymentsToday = await dbContext.SiteDeployments
                .Include(sd => sd.Members)
                .Where(sd => sd.DeploymentDate.Date == today && sd.Status != DeploymentStatus.Cancelled)
                .ToListAsync(stoppingToken);

            var employeeProjectMap = new Dictionary<Guid, Guid>();
            foreach (var dep in deploymentsToday)
            {
                foreach (var member in dep.Members)
                {
                    employeeProjectMap[member.EmployeeId] = dep.ProjectId;
                }
            }

            // Check if today is a scheduled day
            bool isScheduledDay = companyDetails.AutoClockInDays != null && 
                                  companyDetails.AutoClockInDays.Count > 0 && 
                                  companyDetails.AutoClockInDays.Contains(todayDay);

            if (!isScheduledDay && employeeProjectMap.Count == 0)
            {
                _logger.LogInformation($"AutoClockInService: Skipping today ({todayDay}) as it is not a scheduled day and no crews are deployed.");
                return;
            }

            // 2. Get active employees
            IQueryable<Employee> employeeQuery = dbContext.Employees
                .Where(e => e.Status == EmployeeStatus.Active);

            if (!isScheduledDay)
            {
                var deployedEmployeeIds = employeeProjectMap.Keys.ToList();
                employeeQuery = employeeQuery.Where(e => deployedEmployeeIds.Contains(e.Id));
            }

            var activeEmployees = await employeeQuery.ToListAsync(stoppingToken);

            int processedCount = 0;

            foreach (var employee in activeEmployees)
            {
                var shiftStartTime = employee.ShiftStartTime;
                var shiftEndTime = employee.ShiftEndTime;

                // Resolve branch-specific times from the Company Profile (if defined there)
                if (!string.IsNullOrEmpty(employee.Branch) && companyDetails.Branches != null)
                {
                    Branch? branchEnum = employee.Branch.ToLower().Trim() switch
                    {
                        "johannesburg" => Branch.JHB,
                        "jhb" => Branch.JHB,
                        "cape town" => Branch.CPT,
                        "cpt" => Branch.CPT,
                        _ => null
                    };

                    if (branchEnum.HasValue && companyDetails.Branches.TryGetValue(branchEnum.Value, out var branchDetails))
                    {
                        shiftStartTime ??= branchDetails.ShiftStartTime;
                        shiftEndTime ??= branchDetails.ShiftEndTime;
                    }
                }

                // Default fallback if still null
                shiftStartTime ??= new TimeSpan(7, 0, 0);
                shiftEndTime ??= new TimeSpan(16, 45, 0);
                
                // Get V1 and V2 records for today
                var existingRecord = await dbContext.AttendanceRecords
                    .FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.Date.Date == today, stoppingToken);

                var v2Timesheet = await dbContext.DailyTimesheets
                    .FirstOrDefaultAsync(t => t.EmployeeId == employee.Id && t.Date == today, stoppingToken);

                bool madeChanges = false;

                Guid? targetProjectId = null;
                if (employeeProjectMap.TryGetValue(employee.Id, out var projId))
                {
                    targetProjectId = projId;
                }

                if (existingRecord == null)
                {
                    // If they don't have a record today, check if we should auto clock-in
                    if (shiftStartTime != null && currentTime >= shiftStartTime.Value)
                    {
                        var inTime = today.Add(shiftStartTime.Value);
                        
                        // V1
                        var record = new AttendanceRecord
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = employee.Id,
                            Date = today,
                            ProjectId = targetProjectId,
                            Branch = employee.Branch,
                            CheckInTime = inTime,
                            Status = AttendanceStatus.Present,
                            IsAutoClockIn = true,
                            Notes = "Auto Clock-In generated by system."
                        };
                        dbContext.AttendanceRecords.Add(record);
                        
                        // V2
                        if (v2Timesheet == null)
                        {
                            v2Timesheet = new DailyTimesheet
                            {
                                Id = Guid.NewGuid(),
                                EmployeeId = employee.Id,
                                Date = today,
                                FirstInTime = inTime,
                                Status = TimesheetStatus.Present
                            };
                            dbContext.DailyTimesheets.Add(v2Timesheet);
                        }

                        // V2 Immutable Event
                        var clockingEvent = new ClockingEvent
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = employee.Id,
                            Timestamp = inTime,
                            EventType = ClockEventType.ClockIn,
                            Source = "AutoService"
                        };
                        dbContext.ClockingEvents.Add(clockingEvent);

                        madeChanges = true;
                    }
                }
                else
                {
                    if (targetProjectId.HasValue && existingRecord.ProjectId == null)
                    {
                        existingRecord.ProjectId = targetProjectId.Value;
                        madeChanges = true;
                    }

                    // If they have an open record today, check if we should auto clock-out
                    if (existingRecord.CheckOutTime == null && shiftEndTime != null && currentTime >= shiftEndTime.Value)
                    {
                        var outTime = today.Add(shiftEndTime.Value);
                        
                        // V1
                        existingRecord.CheckOutTime = outTime;
                        if (existingRecord.CheckInTime.HasValue)
                        {
                            var duration = outTime - existingRecord.CheckInTime.Value;
                            if (duration.TotalHours > 0)
                            {
                                double lunchHours = 0;
                                var dow = existingRecord.Date.DayOfWeek;
                                bool isWeekend = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
                                bool isHoliday = OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(existingRecord.Date);
                                
                                if (!isWeekend && !isHoliday)
                                {
                                    if (outTime.TimeOfDay >= new TimeSpan(13, 0, 0))
                                    {
                                        lunchHours = 1.0;
                                    }
                                }
                                existingRecord.HoursWorked = Math.Max(0, Math.Round(duration.TotalHours - lunchHours, 2));
                            }
                            else
                            {
                                existingRecord.HoursWorked = 0;
                            }
                        }
                        else
                        {
                            existingRecord.HoursWorked = 0;
                        }
                        
                        if (string.IsNullOrEmpty(existingRecord.Notes))
                            existingRecord.Notes = "Auto Clock-Out generated by system.";
                        else
                            existingRecord.Notes += " | Auto Clock-Out generated by system.";
                            
                        // V2
                        if (v2Timesheet != null && v2Timesheet.LastOutTime == null)
                        {
                            v2Timesheet.LastOutTime = outTime;
                            
                            // Rough calculation for V2 auto-checkout
                            if (v2Timesheet.FirstInTime.HasValue)
                            {
                                v2Timesheet.CalculatedHours = (decimal)(outTime - v2Timesheet.FirstInTime.Value).TotalHours;
                                v2Timesheet.WageEstimated = v2Timesheet.CalculatedHours * (decimal)employee.HourlyRate;
                            }
                        }

                        // V2 Immutable Event
                        var clockingEvent = new ClockingEvent
                        {
                            Id = Guid.NewGuid(),
                            EmployeeId = employee.Id,
                            Timestamp = outTime,
                            EventType = ClockEventType.ClockOut,
                            Source = "AutoService"
                        };
                        dbContext.ClockingEvents.Add(clockingEvent);
                            
                        madeChanges = true;
                    }
                }

                if (madeChanges)
                {
                    processedCount++;
                }
            }

            if (processedCount > 0)
            {
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation($"AutoClockInService: Automatically processed {processedCount} clock-in/out events.");
            }
        }
    }
}
