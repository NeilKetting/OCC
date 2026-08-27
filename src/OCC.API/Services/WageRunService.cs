using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    /// <summary>
    /// Core wage-run generation and processing service.
    /// Supports Standard (Weekly/Fortnightly/Monthly) and Ad-Hoc ("Mamparra") advance wage runs,
    /// dynamic pay frequencies, automatic advance recovery, and wage settings integration.
    /// </summary>
    public class WageRunService : IWageRunService
    {
        private readonly AppDbContext _context;
        private readonly IWageCalculationService _wageCalc;
        private readonly IConfiguration _configuration;

        public WageRunService(AppDbContext context, IWageCalculationService wageCalc, IConfiguration configuration)
        {
            _context = context;
            _wageCalc = wageCalc;
            _configuration = configuration;
        }

        public async Task<WageRun> GenerateDraftAsync(WageRun request)
        {
            var runDate = DateTime.Now.Date;
            var branchEnum = request.Branch.ToBranchEnum();
            bool isCapeTown = branchEnum == Branch.CPT;

            // Load System Wage Settings (or fallback defaults if not created yet)
            var settings = await _context.WageSettings.FirstOrDefaultAsync() ?? new WageSettings();

            // Determine PayFrequency: use request value if specified, else derive from branch settings
            var frequency = request.PayFrequency;
            if (string.Equals(request.PayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
            {
                frequency = PayFrequency.Monthly;
            }
            else if (request.PayFrequency == PayFrequency.Weekly || isCapeTown)
            {
                frequency = PayFrequency.Weekly;
            }
            else if (request.PayFrequency == PayFrequency.Fortnightly || (!isCapeTown && request.Branch != "All"))
            {
                frequency = PayFrequency.Fortnightly;
            }

            // 1. DUPLICATION CHECK: Prevent generating if a FINALIZED run already exists
            var existingRun = await _context.WageRuns
                .AnyAsync(w => w.StartDate == request.StartDate && 
                               w.EndDate == request.EndDate && 
                               w.Branch == request.Branch && 
                               w.PayType == request.PayType &&
                               w.RunType == request.RunType &&
                               (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid));
            
            if (existingRun)
            {
                throw new ArgumentException($"A Finalized Wage Run already exists for {request.Branch} ({request.PayType}, {request.RunType}) between {request.StartDate:yyyy-MM-dd} and {request.EndDate:yyyy-MM-dd}. Please delete the finalized run first if you need to regenerate.");
            }
            
            // 2. Create the Draft Shell
            var draftRun = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                RunDate = runDate,
                Status = WageRunStatus.Draft,
                PayType = request.PayType,
                Branch = request.Branch,
                RunType = request.RunType,
                PayFrequency = frequency,
                Notes = request.Notes
            };

            // 3. Fetch Active Staff matching the PayType
            var rateType = RateType.Hourly;
            if (!string.IsNullOrEmpty(request.PayType) && Enum.TryParse<RateType>(request.PayType, out var parsedType))
            {
                rateType = parsedType;
            }

            var employeesQuery = _context.Employees
                .Where(e => e.Status == EmployeeStatus.Active && e.RateType == rateType);
                
            if (!string.IsNullOrEmpty(request.Branch) && !request.Branch.Equals(BranchConstants.All, StringComparison.OrdinalIgnoreCase))
            {
                if (branchEnum == Branch.JHB)
                {
                    employeesQuery = employeesQuery.Where(e => e.Branch == BranchConstants.Johannesburg || e.Branch == BranchConstants.JHB);
                }
                else if (branchEnum == Branch.CPT)
                {
                    employeesQuery = employeesQuery.Where(e => e.Branch == BranchConstants.CapeTown || e.Branch == BranchConstants.CPT);
                }
                else
                {
                    employeesQuery = employeesQuery.Where(e => e.Branch == request.Branch);
                }
            }

            var employees = await employeesQuery.ToListAsync();

            // BIBC Rate from WageSettings
            decimal bibcRate = settings.BibcRatePerDay;
            
            // Calculate gas split per housed employee
            var housedEmployees = employees.Where(e => e.LivesInCompanyHousing).ToList();
            decimal gasPerPerson = 0;
            if (housedEmployees.Count > 0 && request.InputTotalGasCharge > 0)
            {
                gasPerPerson = request.InputTotalGasCharge / housedEmployees.Count;
            }

            // Load CompanyProfile to resolve branch shift defaults
            CompanyDetails? companyDetails = null;
            var profileSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "CompanyProfile");
            if (profileSetting != null && !string.IsNullOrEmpty(profileSetting.Value))
            {
                companyDetails = JsonSerializer.Deserialize<CompanyDetails>(profileSetting.Value);
            }

            // 4. Fetch Attendance for the Period and any historical unprocessed paid/leave records
            var reqStart = request.StartDate.Date;
            var reqEnd = request.EndDate.Date;

            var attendance = await _context.AttendanceRecords
                .Where(a => (a.PaidWageRunId == null || a.PaidWageRunId == request.Id) && 
                            ((a.Date.Date >= reqStart && a.Date.Date <= reqEnd) ||
                             (a.Date.Date < reqStart && 
                              (a.Status == AttendanceStatus.Sick || 
                               a.Status == AttendanceStatus.LeaveAuthorized || 
                               (a.PaidLeaveHours != null && a.PaidLeaveHours > 0)))))
                .ToListAsync();

            var publicHolidayDates = (await _context.PublicHolidays
                .Where(ph => ph.IsActive)
                .Select(ph => ph.Date.Date)
                .ToListAsync())
                .ToHashSet();

            // Fetch Active Loans that start on or before the end date of the wage run period
            var activeLoans = await _context.EmployeeLoans
                .Where(l => l.IsActive && l.OutstandingBalance > 0 && l.StartDate.Date <= request.EndDate.Date)
                .ToListAsync();

            // Fetch Previous Finalized Run (for Variance) - MUST BE BRANCH SPECIFIC
            var lastRun = await _context.WageRuns
                .Include(w => w.Lines)
                .Where(w => (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid) && 
                            (string.IsNullOrEmpty(request.PayType) || w.PayType == request.PayType || w.PayType == null) &&
                            w.EndDate < request.StartDate &&
                            (w.Branch == request.Branch || w.Branch == BranchConstants.All ||
                             (branchEnum == Branch.JHB && (w.Branch == BranchConstants.JHB || w.Branch == BranchConstants.Johannesburg)) ||
                             (branchEnum == Branch.CPT && (w.Branch == BranchConstants.CPT || w.Branch == BranchConstants.CapeTown))))
                .OrderByDescending(w => w.EndDate)
                .FirstOrDefaultAsync();

            // Fetch Prior Finalized Ad-Hoc ("Mamparra") Runs that require advance recovery
            var unrecoveredAdHocRuns = await _context.WageRuns
                .Include(w => w.Lines)
                .Where(w => w.RunType == WageRunType.AdHocAdvance &&
                            (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid) &&
                            w.StartDate >= (lastRun != null ? lastRun.StartDate : DateTime.MinValue) &&
                            (w.Branch == request.Branch || w.Branch == BranchConstants.All ||
                             (branchEnum == Branch.JHB && (w.Branch == BranchConstants.JHB || w.Branch == BranchConstants.Johannesburg)) ||
                             (branchEnum == Branch.CPT && (w.Branch == BranchConstants.CPT || w.Branch == BranchConstants.CapeTown))))
                .OrderByDescending(w => w.StartDate)
                .ToListAsync();

            // Pre-load prepaid window leave requests and attendance records
            var prepaidStart = request.StartDate.AddDays(-2).Date;
            var prepaidEnd = request.StartDate.AddDays(-1).Date;

            var activeLeaveRequests = await _context.LeaveRequests
                .Where(lr => (lr.Status == LeaveStatus.Approved || lr.Status == LeaveStatus.Pending) &&
                             lr.StartDate <= request.EndDate.AddDays(2) &&
                             lr.EndDate >= prepaidStart.AddDays(-2))
                .ToListAsync();

            var prepaidAttendanceRecords = await _context.AttendanceRecords
                .Where(ar => ar.Date >= prepaidStart.Date &&
                             ar.Date < prepaidEnd.Date.AddDays(1))
                .ToListAsync();

            foreach (var emp in employees)
            {
                var line = new WageRunLine
                {
                    Id = Guid.NewGuid(),
                    WageRunId = draftRun.Id,
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    EmployeeNumber = emp.EmployeeNumber,
                    Branch = emp.Branch ?? "",
                    BankName = emp.BankName,
                    BankAccountNumber = emp.AccountNumber,
                    Comments = string.Empty,
                    EmploymentType = emp.EmploymentType.ToString(),
                    HourlyRate = (decimal)emp.HourlyRate,
                    DeductionGas = 0,
                    DeductionWashing = 0,
                    IncentiveSupervisor = 0,
                    DeductionAdvanceRecovery = 0,
                    IsCompanyHoused = emp.LivesInCompanyHousing,
                    IsSupervisor = (emp.Role == EmployeeRole.Supervisor || emp.Role == EmployeeRole.SiteManager ||
                                    emp.Role == EmployeeRole.BuildingSupervisor || emp.Role == EmployeeRole.PlasterSupervisor ||
                                    emp.Role == EmployeeRole.ShopfittingSupervisor || emp.Role == EmployeeRole.PaintingSupervisor ||
                                    emp.Role == EmployeeRole.LabourSupervisor),
                    IsBibc = emp.IsBibc
                };

                // Supervisor Incentive: check previous run or fallback to default
                decimal supervisorFee = 0;
                if (lastRun != null)
                {
                    var lastLine = lastRun.Lines.FirstOrDefault(l => l.EmployeeId == emp.Id);
                    if (lastLine != null && lastLine.IncentiveSupervisor > 0)
                    {
                        supervisorFee = lastLine.IncentiveSupervisor;
                    }
                }

                if (supervisorFee == 0 && line.IsSupervisor)
                {
                    supervisorFee = request.InputDefaultSupervisorFee > 0 
                        ? request.InputDefaultSupervisorFee 
                        : settings.DefaultSupervisorFee;
                }

                line.IncentiveSupervisor = supervisorFee;

                // Housing Gas & Washing Deduction
                if (emp.LivesInCompanyHousing)
                {
                    line.DeductionGas = gasPerPerson;
                    decimal washingFee = request.InputCompanyHousingWashingFee > 0 
                        ? request.InputCompanyHousingWashingFee 
                        : settings.DefaultCompanyHousingWashingFee;
                    if (washingFee > 0)
                    {
                        line.DeductionWashing = washingFee;
                    }
                }

                // Check for Ad-Hoc ("Mamparra") Advance Recovery
                if (settings.AutoRecoverAdHocAdvances && request.RunType == WageRunType.Standard && unrecoveredAdHocRuns.Any())
                {
                    foreach (var adHocRun in unrecoveredAdHocRuns)
                    {
                        var adHocLine = adHocRun.Lines.FirstOrDefault(l => l.EmployeeId == emp.Id);
                        if (adHocLine != null && adHocLine.NetPay > 0)
                        {
                            line.DeductionAdvanceRecovery += adHocLine.NetPay;
                            string advNote = $"Adv Recovery ({adHocRun.StartDate:dd/MM}): -R{adHocLine.NetPay:F2}";
                            line.Comments = string.IsNullOrWhiteSpace(line.Comments) ? advNote : $"{line.Comments} | {advNote}";
                        }
                    }
                }

                // A. Calculate Normal & Overtime Hours (Actual)
                var empAttendance = attendance
                    .Where(a => a.EmployeeId == emp.Id)
                    .OrderBy(a => a.Date)
                    .ToList();

                var empLeaveRequests = activeLeaveRequests
                    .Where(lr => lr.EmployeeId == emp.Id)
                    .ToList();

                var week1End = request.StartDate.AddDays(6);
                
                var distinctDaysW1 = new HashSet<DateTime>();
                var distinctDaysW2 = new HashSet<DateTime>();

                var empForCalc = emp;
                if ((emp.ShiftStartTime == null || emp.ShiftEndTime == null) && companyDetails != null)
                {
                    var branchKey = emp.Branch?.Contains("Cape", StringComparison.OrdinalIgnoreCase) == true
                        ? Branch.CPT
                        : Branch.JHB;

                    if (companyDetails?.Branches != null && companyDetails.Branches.TryGetValue(branchKey, out var branchDetails))
                    {
                        empForCalc = new Employee
                        {
                            Id                    = emp.Id,
                            FirstName             = emp.FirstName,
                            LastName              = emp.LastName,
                            EmployeeNumber        = emp.EmployeeNumber,
                            Role                  = emp.Role,
                            Branch                = emp.Branch ?? "",
                            EmploymentType        = emp.EmploymentType,
                            RateType              = emp.RateType,
                            HourlyRate            = emp.HourlyRate,
                            LivesInCompanyHousing = emp.LivesInCompanyHousing,
                            ShiftStartTime        = emp.ShiftStartTime ?? branchDetails.ShiftStartTime,
                            ShiftEndTime          = emp.ShiftEndTime ?? branchDetails.ShiftEndTime
                        };
                    }
                }

                double dailyHours = 9.0;
                if (empForCalc.ShiftStartTime.HasValue && empForCalc.ShiftEndTime.HasValue)
                {
                    dailyHours = (empForCalc.ShiftEndTime.Value - empForCalc.ShiftStartTime.Value).TotalHours;
                    if (empForCalc.ShiftEndTime.Value.Hours >= settings.LunchEndHourThreshold)
                    {
                        dailyHours -= 1.0;
                    }
                    if (dailyHours < 0) dailyHours = 0;
                }

                // Pre-process Approved Leave Requests: Ensure dates covered by approved leave requests have attendance records
                foreach (var lr in empLeaveRequests)
                {
                    if (lr.Status != LeaveStatus.Approved && lr.Status != LeaveStatus.Pending) continue;

                    var lStart = lr.StartDate.Date < request.StartDate.Date ? request.StartDate.Date : lr.StartDate.Date;
                    var lEnd = lr.EndDate.Date > request.EndDate.Date ? request.EndDate.Date : lr.EndDate.Date;

                    for (var d = lStart; d <= lEnd; d = d.AddDays(1))
                    {
                        if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                        if (publicHolidayDates.Contains(d)) continue;

                        if (!empAttendance.Any(a => a.Date.Date == d.Date))
                        {
                            bool isSick = lr.LeaveType == LeaveType.Sick;
                            bool isUnpaid = lr.IsUnpaid || (isSick && emp.SickLeaveBalance <= 0 && lr.PaidDays == 0);
                            bool isPaidSick = isSick && !isUnpaid;
                            bool isPaidLeave = !isSick && !isUnpaid;

                            AttendanceStatus status = isPaidSick ? AttendanceStatus.Sick :
                                                       (isPaidLeave ? AttendanceStatus.LeaveAuthorized :
                                                       (isSick ? AttendanceStatus.UnpaidSick : AttendanceStatus.UnpaidLeave));

                            empAttendance.Add(new AttendanceRecord
                            {
                                Id = Guid.NewGuid(),
                                EmployeeId = emp.Id,
                                Date = d,
                                Status = status,
                                Branch = emp.Branch ?? "",
                                PaidLeaveHours = (isPaidSick || isPaidLeave) ? dailyHours : 0,
                                Notes = $"Approved Leave ({lr.LeaveType})"
                            });
                        }
                    }
                }
                // Pre-process Public Holidays: Ensure weekday public holidays within period have attendance records
                for (var d = request.StartDate.Date; d <= request.EndDate.Date; d = d.AddDays(1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                    bool isHol = publicHolidayDates.Contains(d) || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d);
                    if (isHol)
                    {
                        bool exists = empAttendance.Any(a => (a.Date.Kind == DateTimeKind.Utc ? a.Date.ToLocalTime().Date : a.Date.Date) == d);
                        if (!exists)
                        {
                            empAttendance.Add(new AttendanceRecord
                            {
                                Id = Guid.NewGuid(),
                                EmployeeId = emp.Id,
                                Date = d,
                                Status = AttendanceStatus.Absent,
                                Branch = emp.Branch ?? "",
                                Notes = "Public Holiday"
                            });
                        }
                    }
                }

                empAttendance = empAttendance.OrderBy(a => a.Date).ToList();

                var leaveDetails = new List<(string Label, string DateStr, double Days)>();

                foreach (var record in empAttendance)
                {
                    DateTime recDate = record.Date.Kind == DateTimeKind.Utc ? record.Date.ToLocalTime().Date : record.Date.Date;
                    var hours = _wageCalc.CalculateHours(record, empForCalc, settings);
                    if (recDate >= request.StartDate.Date)
                    {
                        line.NormalHours += hours.Normal;
                        if (recDate.DayOfWeek == DayOfWeek.Saturday)
                        {
                            line.SaturdayOvertimeHours += hours.Overtime15;
                        }
                        else
                        {
                            line.Overtime15Hours += hours.Overtime15;
                        }

                        bool isHoliday = publicHolidayDates.Contains(recDate) || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(recDate);
                        if (isHoliday)
                        {
                            line.PublicHolidayOvertimeHours += hours.Overtime20;
                        }
                        else
                        {
                            line.Overtime20Hours += hours.Overtime20;
                        }
                        line.LunchDeductionHours += hours.Lunch;
                        
                        bool isPaidAttendanceOrLeave = record.Status == AttendanceStatus.Present || 
                                                       record.Status == AttendanceStatus.Late || 
                                                       record.Status == AttendanceStatus.LeaveEarly ||
                                                       ((record.Status == AttendanceStatus.Sick || record.Status == AttendanceStatus.LeaveAuthorized) && hours.Normal > 0) ||
                                                       (isHoliday && record.Status != AttendanceStatus.UnpaidSick && record.Status != AttendanceStatus.UnpaidLeave);

                        if (isPaidAttendanceOrLeave)
                        {
                            var hasUnpaidLeave = empLeaveRequests.Any(lr => IsUnpaidLeaveForDate(lr, recDate));

                            if (!hasUnpaidLeave || record.Status == AttendanceStatus.Sick || record.Status == AttendanceStatus.LeaveAuthorized || isHoliday)
                            {
                                bool isWeekend = recDate.DayOfWeek == DayOfWeek.Saturday || recDate.DayOfWeek == DayOfWeek.Sunday;
                                if (!isCapeTown || !isWeekend)
                                {
                                    if (recDate <= week1End.Date) distinctDaysW1.Add(recDate);
                                    else distinctDaysW2.Add(recDate);
                                }
                            }
                        }

                        if (record.Status == AttendanceStatus.Absent)
                        {
                            if (isHoliday)
                            {
                                line.VarianceNotes += $"{record.Date:dd/MM}: Paid Holiday (Absent); ";
                            }
                            else
                            {
                                line.VarianceNotes += $"{record.Date:dd/MM}: Absent; ";
                            }
                        }
                        else
                        {
                            var matchingLeave = empLeaveRequests.FirstOrDefault(lr => 
                                (lr.Status == LeaveStatus.Approved || lr.Status == LeaveStatus.Pending) && 
                                lr.StartDate.Date <= recDate && lr.EndDate.Date >= recDate);

                            bool isLeaveRecord = record.Status == AttendanceStatus.Sick || 
                                                 record.Status == AttendanceStatus.LeaveAuthorized || 
                                                 record.Status == AttendanceStatus.UnpaidSick || 
                                                 record.Status == AttendanceStatus.UnpaidLeave || 
                                                 record.Status == AttendanceStatus.UnpaidHalfDay || 
                                                 (record.PaidLeaveHours.HasValue && record.PaidLeaveHours.Value > 0) ||
                                                 (record.UnpaidLeaveHours.HasValue && record.UnpaidLeaveHours.Value > 0) ||
                                                 matchingLeave != null;

                            if (isLeaveRecord)
                            {
                                var (label, days) = GetLeaveDetail(record, matchingLeave, dailyHours, hours);
                                line.VarianceNotes += $"{record.Date:dd/MM}: {label}; ";
                                leaveDetails.Add((label, recDate.ToString("dd/MM"), days));
                            }
                        }
                    }
                    else
                    {
                        double backPayHours = hours.Normal + hours.Overtime15 + hours.Overtime20;
                        if (backPayHours > 0)
                        {
                            line.VarianceHours += backPayHours;
                            string statusDesc = record.Status == AttendanceStatus.Sick ? "Sick" : (record.Status == AttendanceStatus.LeaveAuthorized ? "Leave" : "Worked");
                            line.VarianceNotes += $"Back-pay {record.Date:dd/MM} ({statusDesc} +{backPayHours:F1}h); ";
                        }
                    }
                }

                if (leaveDetails.Count > 0)
                {
                    var groupedNotes = leaveDetails
                        .GroupBy(x => x.Label)
                        .Select(g => 
                        {
                            double totalDays = g.Sum(x => x.Days);
                            var dateList = g.Select(x => x.DateStr).Distinct();
                            string datesFormatted = string.Join(", ", dateList);
                            string daysFormatted = totalDays % 1 == 0 
                                ? $"{totalDays:F0}d" 
                                : $"{totalDays.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}d";
                            string extraInfo = g.Key.StartsWith("Unpaid Sick", StringComparison.OrdinalIgnoreCase) ? " - No leave available" : "";
                            
                            return $"{g.Key} ({daysFormatted}: {datesFormatted}{extraInfo})";
                        });

                    string leaveSummary = string.Join(" | ", groupedNotes);
                    line.Comments = string.IsNullOrWhiteSpace(line.Comments) 
                        ? leaveSummary 
                        : $"{line.Comments} | {leaveSummary}";
                }

                // Monthly Salary Base Working Hours Fallback
                if (emp.RateType == RateType.MonthlySalary || string.Equals(request.PayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
                {
                    double monthStandardHours = 0;
                    for (var d = reqStart; d <= reqEnd; d = d.AddDays(1))
                    {
                        if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d))
                        {
                            monthStandardHours += dailyHours;
                        }
                    }

                    if (monthStandardHours > 0 && line.NormalHours < monthStandardHours)
                    {
                        double unpaidDeductions = 0;
                        foreach (var record in empAttendance)
                        {
                            if (record.Date.Date >= reqStart && record.Date.Date <= reqEnd)
                            {
                                if (record.Status == AttendanceStatus.Absent || record.Status == AttendanceStatus.UnpaidSick || record.Status == AttendanceStatus.UnpaidLeave)
                                {
                                    unpaidDeductions += dailyHours;
                                }
                                else if (record.Status == AttendanceStatus.UnpaidHalfDay)
                                {
                                    unpaidDeductions += (dailyHours / 2.0);
                                }
                            }
                        }

                        line.NormalHours = Math.Max(line.NormalHours, monthStandardHours - unpaidDeductions);
                    }
                }

                line.DaysWorkedWeek1 = 0;
                line.DaysWorkedWeek2 = distinctDaysW1.Count;
                line.DaysWorkedWeek3 = distinctDaysW2.Count;
                line.TotalDaysWorked = distinctDaysW1.Count + distinctDaysW2.Count;

                // B. Calculate Projected Hours (Thursday to Friday window if enabled in settings)
                if (settings.EnableProjectedHours)
                {
                    var projectedStart = DateTime.MinValue;
                    var projectedEnd = DateTime.MinValue;
                    int startOffset = (request.PayFrequency == PayFrequency.Weekly || isCapeTown) ? 0 : 7;
                    int endOffset = (request.PayFrequency == PayFrequency.Weekly) ? 6 : (isCapeTown ? 6 : 13);
                    for (int i = startOffset; i <= endOffset; i++)
                    {
                        var date = request.StartDate.AddDays(i).Date;
                        if (date.DayOfWeek == DayOfWeek.Thursday)
                            projectedStart = date;
                        else if (date.DayOfWeek == DayOfWeek.Friday)
                            projectedEnd = date;
                    }

                    if (projectedStart <= projectedEnd)
                    {
                        for (var d = projectedStart; d <= projectedEnd; d = d.AddDays(1))
                        {
                            var dow = d.DayOfWeek;
                            if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d)) continue;

                            // Do not project for dates that have already passed (actual attendance records are evaluated instead)
                            if (d.Date < runDate) continue;

                            var hasUnpaidProjLeave = empLeaveRequests.Any(lr => IsUnpaidLeaveForDate(lr, d));
                            if (hasUnpaidProjLeave) continue;

                            var record = empAttendance.FirstOrDefault(r => r.Date.Date == d.Date);
                            if (record == null)
                            {
                                line.ProjectedHours += dailyHours;
                            }
                        }
                    }
                }

                int projectedDays = dailyHours > 0 ? (int)Math.Round(line.ProjectedHours / dailyHours) : 0;
                if (request.PayFrequency == PayFrequency.Weekly)
                {
                    line.DaysWorkedWeek2 = distinctDaysW1.Count + distinctDaysW2.Count + projectedDays;
                    line.DaysWorkedWeek3 = 0;
                }
                else if (isCapeTown)
                {
                    line.DaysWorkedWeek2 = distinctDaysW1.Count + projectedDays;
                    line.DaysWorkedWeek3 = distinctDaysW2.Count;
                }
                else
                {
                    line.DaysWorkedWeek2 = distinctDaysW1.Count;
                    line.DaysWorkedWeek3 = distinctDaysW2.Count + projectedDays;
                }
                line.TotalDaysWorked = line.DaysWorkedWeek1 + line.DaysWorkedWeek2 + line.DaysWorkedWeek3;

                // C. Variance Calculation (Advance Recovery for previous run's projected days)
                double leaveDeductionDays = 0;
                double leaveDeductionHours = 0;
                int prepaidWorkingDays = 0;

                var empAttendanceRecords = prepaidAttendanceRecords
                    .Where(ar => ar.EmployeeId == emp.Id)
                    .ToList();

                var priorLine = lastRun?.Lines?.FirstOrDefault(l => l.EmployeeId == emp.Id);
                double standardDayHours = dailyHours > 0 ? dailyHours : 8.75;
                // If prior line exists and had projected hours > 0, use it.
                // Otherwise (e.g. prior line has 0 projected hours or prior run missing), default to 2 working days (17.5 hours) for Thursday and Friday of Week 0 so unpaid leave/absences are properly deducted.
                double maxProjectedHoursToDeduct = (priorLine != null && priorLine.ProjectedHours > 0) ? (double)priorLine.ProjectedHours : (standardDayHours * 2.0);

                double remainingProjectedHours = maxProjectedHoursToDeduct;
                var absentDatesList = new List<string>();

                for (var d = prepaidEnd; d >= prepaidStart; d = d.AddDays(-1))
                {
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d))
                        continue;

                    prepaidWorkingDays++;

                    double dayHours = standardDayHours;
                    TimeSpan? startTime = emp.ShiftStartTime;
                    TimeSpan? endTime = emp.ShiftEndTime;

                    if (startTime == null || endTime == null)
                    {
                        var bEnum = emp.Branch.ToBranchEnum() ?? Branch.JHB;
                        if (companyDetails != null && companyDetails.Branches != null && companyDetails.Branches.TryGetValue(bEnum, out var branchDetails))
                        {
                            startTime = branchDetails.ShiftStartTime;
                            endTime = branchDetails.ShiftEndTime;
                        }
                    }

                    if (startTime != null && endTime != null)
                    {
                        var duration = endTime.Value - startTime.Value;
                        double rawHours = duration.TotalHours;
                        if (rawHours > 0)
                        {
                            double lunch = 1.0;
                            var dow = d.DayOfWeek;
                            bool isWeekend = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
                            bool isHoliday = OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d);
                            if (isWeekend || isHoliday)
                            {
                                lunch = 0;
                            }
                            dayHours = Math.Max(0, rawHours - lunch);
                        }
                    }

                    if (remainingProjectedHours >= dayHours - 0.01)
                    {
                        remainingProjectedHours -= dayHours;

                        var isAbsent = empLeaveRequests.Any(lr => IsUnpaidLeaveForDate(lr, d));

                        if (!isAbsent)
                        {
                            var attendanceRecord = empAttendanceRecords.FirstOrDefault(ar =>
                                ar.Date.Date == d.Date ||
                                (ar.Date.Kind == DateTimeKind.Utc ? ar.Date.ToLocalTime().Date : ar.Date.Date) == d.Date);

                            if (attendanceRecord != null && (
                                attendanceRecord.Status == AttendanceStatus.Absent || 
                                attendanceRecord.Status == AttendanceStatus.UnpaidSick || 
                                attendanceRecord.Status == AttendanceStatus.UnpaidLeave || 
                                attendanceRecord.Status == AttendanceStatus.UnpaidHalfDay ||
                                (attendanceRecord.HoursWorked == 0 && (attendanceRecord.PaidLeaveHours == null || attendanceRecord.PaidLeaveHours == 0) && attendanceRecord.Status != AttendanceStatus.Present)))
                            {
                                isAbsent = true;
                            }
                        }

                        if (isAbsent)
                        {
                            leaveDeductionDays++;
                            leaveDeductionHours += dayHours;
                            absentDatesList.Add(d.ToString("dd/MM"));
                        }
                    }
                }

                if (leaveDeductionDays > 0)
                {
                    line.VarianceHours = -leaveDeductionHours;
                    absentDatesList.Reverse();
                    string dateStr = absentDatesList.Any() ? string.Join(", ", absentDatesList) : $"{leaveDeductionDays:F0} day(s)";
                    string noteText = $"Adv Adj ({dateStr}): Absent -{leaveDeductionDays:F0} day(s)";

                    if (string.IsNullOrWhiteSpace(line.VarianceNotes))
                        line.VarianceNotes = noteText + "; ";
                    else if (!line.VarianceNotes.Contains("Adv Adj"))
                        line.VarianceNotes += noteText + "; ";

                    if (string.IsNullOrWhiteSpace(line.Comments))
                        line.Comments = noteText;
                    else if (!line.Comments.Contains("Adv Adj"))
                        line.Comments += " | " + noteText;

                    line.DaysWorkedWeek1 = -leaveDeductionDays;
                }

                line.TotalDaysWorked = line.DaysWorkedWeek1 + line.DaysWorkedWeek2 + line.DaysWorkedWeek3;

                // Calculate BIBC Amount
                if (emp.IsBibc && emp.Branch.ToBranchEnum() == Branch.CPT)
                {
                    line.BibcAmount = bibcRate * (decimal)line.TotalDaysWorked;
                }
                else
                {
                    line.BibcAmount = 0m;
                }

                // D. Total Wage Calculation
                var calculatedWage = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours) * line.HourlyRate +
                                     (decimal)(line.Overtime15Hours + line.SaturdayOvertimeHours) * line.HourlyRate * 1.5m +
                                     (decimal)(line.Overtime20Hours + line.PublicHolidayOvertimeHours) * line.HourlyRate * 2.0m;
                line.TotalWage = Math.Max(0m, calculatedWage);
                    
                // E. Loans (deducted according to loan agreement frequency and respecting loan start date)
                var empLoans = activeLoans.Where(l => l.EmployeeId == emp.Id).ToList();
                decimal totalLoanDeduction = 0;
                foreach (var loan in empLoans)
                {
                    // Ensure the loan start date has arrived (on or before the pay period end date)
                    if (loan.StartDate.Date > request.EndDate.Date)
                    {
                        continue;
                    }

                    string loanFreq = "";
                    if (!string.IsNullOrEmpty(loan.Notes) && loan.Notes.StartsWith("[Term:") && loan.Notes.Contains("]"))
                    {
                        int termEnd = loan.Notes.IndexOf(']');
                        string header = loan.Notes.Substring(1, termEnd - 1);
                        string[] parts = header.Split(',');
                        foreach (var part in parts)
                        {
                            if (part.Contains("Term:"))
                            {
                                loanFreq = part.Replace("Term:", "").Trim();
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(loanFreq))
                    {
                        loanFreq = emp.RateType == RateType.Hourly ? "Fortnightly" : "Monthly";
                    }

                    bool matchesRun = (rateType == RateType.Hourly && (loanFreq == "Fortnightly" || loanFreq == "Weekly")) ||
                                      (rateType == RateType.MonthlySalary && loanFreq == "Monthly");

                    if (matchesRun)
                    {
                        var deduction = loan.MonthlyInstallment;
                        if (deduction > loan.OutstandingBalance) deduction = loan.OutstandingBalance;
                        totalLoanDeduction += deduction;
                    }
                }
                line.DeductionLoan = totalLoanDeduction;

                draftRun.Lines.Add(line);
            }

            return draftRun;
        }

        private async Task PerformPreFinalizationBackupAsync()
        {
            try
            {
                var dbConnection = _context.Database.GetDbConnection();
                var dbName = dbConnection.Database;
                if (string.IsNullOrEmpty(dbName))
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(dbConnection.ConnectionString);
                    dbName = builder.InitialCatalog;
                }

                var backupPath = _configuration.GetValue<string>("Backup:Path") ?? @"C:\OCCBackups";
                if (!System.IO.Directory.Exists(backupPath))
                {
                    System.IO.Directory.CreateDirectory(backupPath);
                }

                var backupFileName = System.IO.Path.Combine(backupPath, $"{dbName}_PreFinalize_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                
#pragma warning disable EF1002
                await _context.Database.ExecuteSqlRawAsync(
                    $"BACKUP DATABASE [{dbName}] TO DISK = {{0}} WITH FORMAT, NAME = {{1}};", 
                    backupFileName, "Pre-Finalization Backup");
#pragma warning restore EF1002
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pre-finalization backup failed: {ex.Message}");
            }
        }

        public async Task<WageRun> FinalizeRunAsync(WageRun run)
        {
            await PerformPreFinalizationBackupAsync();

            var existing = await _context.WageRuns.Include(w => w.Lines).FirstOrDefaultAsync(w => w.Id == run.Id);
            if (existing != null)
            {
                // Reverse old loan updates for existing run
                foreach (var line in existing.Lines)
                {
                    if (line.DeductionLoan > 0)
                    {
                        var empLoans = await _context.EmployeeLoans
                            .Where(l => l.EmployeeId == line.EmployeeId)
                            .OrderByDescending(l => l.StartDate)
                            .ToListAsync();
                            
                        decimal remainingToRestore = line.DeductionLoan;
                        foreach (var loan in empLoans)
                        {
                            if (remainingToRestore <= 0) break;
                            
                            decimal maxRestore = loan.PrincipalAmount - loan.OutstandingBalance;
                            if (maxRestore > 0)
                            {
                                decimal restoreAmount = Math.Min(maxRestore, remainingToRestore);
                                loan.OutstandingBalance += restoreAmount;
                                remainingToRestore -= restoreAmount;
                                
                                loan.IsActive = true;
                                loan.EndDate = null;
                            }
                        }
                    }
                }

                existing.Notes = run.Notes;
                existing.Status = WageRunStatus.Finalized;
                existing.RunDate = run.RunDate != default ? run.RunDate : DateTime.Now.Date;
                existing.RunType = run.RunType;
                existing.PayFrequency = run.PayFrequency;
                existing.InputTotalGasCharge = run.InputTotalGasCharge;
                existing.InputDefaultSupervisorFee = run.InputDefaultSupervisorFee;
                existing.InputCompanyHousingWashingFee = run.InputCompanyHousingWashingFee;

                var incomingIds = run.Lines.Select(l => l.Id).Where(id => id != Guid.Empty).ToHashSet();
                var linesToRemove = existing.Lines.Where(l => !incomingIds.Contains(l.Id)).ToList();
                _context.WageRunLines.RemoveRange(linesToRemove);

                foreach (var incomingLine in run.Lines)
                {
                    var existingLine = incomingLine.Id != Guid.Empty 
                        ? existing.Lines.FirstOrDefault(l => l.Id == incomingLine.Id) 
                        : null;

                    if (existingLine != null)
                    {
                        _context.Entry(existingLine).CurrentValues.SetValues(incomingLine);
                        existingLine.WageRunId = existing.Id;
                    }
                    else
                    {
                        if (incomingLine.Id == Guid.Empty) incomingLine.Id = Guid.NewGuid();
                        incomingLine.WageRunId = existing.Id;
                        existing.Lines.Add(incomingLine);
                    }
                }
                run = existing;
            }
            else
            {
                foreach (var line in run.Lines)
                {
                    if (line.Id == Guid.Empty) line.Id = Guid.NewGuid();
                    line.WageRunId = run.Id;
                }
                run.Status = WageRunStatus.Finalized;
                _context.WageRuns.Add(run);
            }

            // Update employee loan outstanding balances
            foreach (var line in run.Lines)
            {
                if (line.DeductionLoan > 0)
                {
                    var rateType = RateType.Hourly;
                    if (!string.IsNullOrEmpty(run.PayType) && Enum.TryParse<RateType>(run.PayType, out var parsedType))
                    {
                        rateType = parsedType;
                    }

                    var activeLoans = await _context.EmployeeLoans
                        .Where(l => l.EmployeeId == line.EmployeeId && l.IsActive && l.OutstandingBalance > 0 && l.StartDate.Date <= run.EndDate.Date)
                        .OrderBy(l => l.StartDate)
                        .ToListAsync();

                    var matchingLoans = new List<EmployeeLoan>();
                    foreach (var loan in activeLoans)
                    {
                        // Ensure loan start date is on or before the wage run end date
                        if (loan.StartDate.Date > run.EndDate.Date)
                        {
                            continue;
                        }

                        string loanFreq = "";
                        if (!string.IsNullOrEmpty(loan.Notes) && loan.Notes.StartsWith("[Term:") && loan.Notes.Contains("]"))
                        {
                            int termEnd = loan.Notes.IndexOf(']');
                            string header = loan.Notes.Substring(1, termEnd - 1);
                            string[] parts = header.Split(',');
                            foreach (var part in parts)
                            {
                                if (part.Contains("Term:"))
                                {
                                    loanFreq = part.Replace("Term:", "").Trim();
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(loanFreq))
                        {
                            var employee = await _context.Employees.FindAsync(loan.EmployeeId);
                            loanFreq = employee?.RateType == RateType.Hourly ? "Fortnightly" : "Monthly";
                        }

                        bool matchesRun = (rateType == RateType.Hourly && (loanFreq == "Fortnightly" || loanFreq == "Weekly")) ||
                                          (rateType == RateType.MonthlySalary && loanFreq == "Monthly");

                        if (matchesRun)
                        {
                            matchingLoans.Add(loan);
                        }
                    }

                    decimal remainingDeduction = line.DeductionLoan;
                    foreach (var loan in matchingLoans)
                    {
                        if (remainingDeduction <= 0) break;

                        if (loan.OutstandingBalance >= remainingDeduction)
                        {
                            loan.OutstandingBalance -= remainingDeduction;
                            remainingDeduction = 0;
                        }
                        else
                        {
                            remainingDeduction -= loan.OutstandingBalance;
                            loan.OutstandingBalance = 0;
                        }

                        if (loan.OutstandingBalance == 0)
                        {
                            loan.IsActive = false;
                            loan.EndDate = run.EndDate;
                        }
                    }
                }
            }

            // Mark ALL processed attendance records in period (including UnpaidLeave, UnpaidSick, Absent) as paid by this run
            var attendanceEndFinal = run.EndDate != default ? run.EndDate : DateTime.Now.Date;

            var employeeIds = run.Lines.Select(l => l.EmployeeId).ToList();

            var attendanceRecordsToFinalize = await _context.AttendanceRecords
                .Where(a => a.PaidWageRunId == null && 
                            a.EmployeeId != null &&
                            employeeIds.Contains(a.EmployeeId.Value) &&
                            ((a.Date >= run.StartDate && a.Date <= attendanceEndFinal) ||
                             (a.Date < run.StartDate && 
                              (a.Status == AttendanceStatus.Sick || 
                               a.Status == AttendanceStatus.LeaveAuthorized || 
                               a.Status == AttendanceStatus.UnpaidSick || 
                               a.Status == AttendanceStatus.UnpaidLeave || 
                               a.Status == AttendanceStatus.Absent || 
                               (a.PaidLeaveHours != null && a.PaidLeaveHours > 0)))))
                .ToListAsync();

            foreach (var record in attendanceRecordsToFinalize)
            {
                record.PaidWageRunId = run.Id;
            }

            await _context.SaveChangesAsync();
            return run;
        }

        private static bool IsUnpaidLeaveForDate(LeaveRequest lr, DateTime targetDate)
        {
            var target = targetDate.Date;

            var start = lr.StartDate.Kind == DateTimeKind.Utc ? lr.StartDate.ToLocalTime().Date : lr.StartDate.Date;
            var end = lr.EndDate.Kind == DateTimeKind.Utc ? lr.EndDate.ToLocalTime().Date : lr.EndDate.Date;

            bool dateMatches = (target >= start && target <= end) ||
                               (target >= lr.StartDate.Date && target <= lr.EndDate.Date);

            if (!dateMatches) return false;

            return lr.IsUnpaid ||
                   lr.LeaveType == LeaveType.Unpaid ||
                   lr.LeaveType == LeaveType.AbsentWithoutLeave ||
                   lr.UnpaidDays > 0 ||
                   (lr.LeaveType != LeaveType.Annual && lr.LeaveType != LeaveType.Sick && lr.PaidDays == 0);
        }

        private static (string Label, double Days) GetLeaveDetail(AttendanceRecord record, LeaveRequest? matchingLeave, double dailyHours, HoursBreakdown hours)
        {
            bool isSick = record.Status == AttendanceStatus.Sick 
                       || record.Status == AttendanceStatus.UnpaidSick 
                       || (matchingLeave != null && matchingLeave.LeaveType == LeaveType.Sick);

            bool isUnpaid = record.Status == AttendanceStatus.UnpaidLeave 
                         || record.Status == AttendanceStatus.UnpaidSick 
                         || record.Status == AttendanceStatus.UnpaidHalfDay 
                         || (matchingLeave != null && matchingLeave.IsUnpaid)
                         || (isSick && hours.Normal == 0 && (!record.PaidLeaveHours.HasValue || record.PaidLeaveHours.Value == 0));

            bool isPaid = !isUnpaid;

            string baseCategory = isSick 
                ? (isPaid ? "Paid Sick" : "Unpaid Sick") 
                : (isPaid ? "Paid Leave" : "Unpaid Leave");

            string periodModifier = string.Empty;
            double daysCount = 1.0;

            if (matchingLeave != null)
            {
                if (matchingLeave.DurationType == LeaveDurationType.MorningHalfDay)
                {
                    periodModifier = " - Half Day (Morning)";
                    daysCount = 0.5;
                }
                else if (matchingLeave.DurationType == LeaveDurationType.AfternoonHalfDay)
                {
                    periodModifier = " - Half Day (Afternoon)";
                    daysCount = 0.5;
                }
                else if (matchingLeave.LeaveType == LeaveType.HalfDay || matchingLeave.NumberOfDays == 0.5)
                {
                    periodModifier = " - Half Day";
                    daysCount = 0.5;
                }
                else if (matchingLeave.DurationType == LeaveDurationType.Hourly)
                {
                    periodModifier = matchingLeave.HoursRequested.HasValue ? $" - Hourly ({matchingLeave.HoursRequested.Value:F1}h)" : " - Hourly";
                    daysCount = (matchingLeave.HoursRequested ?? 0) / (dailyHours > 0 ? dailyHours : 8.5);
                }
            }

            if (string.IsNullOrEmpty(periodModifier))
            {
                string notes = record.Notes ?? string.Empty;
                if (notes.Contains("MorningHalfDay", StringComparison.OrdinalIgnoreCase) || notes.Contains("Morning", StringComparison.OrdinalIgnoreCase))
                {
                    periodModifier = " - Half Day (Morning)";
                    daysCount = 0.5;
                }
                else if (notes.Contains("AfternoonHalfDay", StringComparison.OrdinalIgnoreCase) || notes.Contains("Afternoon", StringComparison.OrdinalIgnoreCase))
                {
                    periodModifier = " - Half Day (Afternoon)";
                    daysCount = 0.5;
                }
                else if (record.Status == AttendanceStatus.UnpaidHalfDay 
                      || notes.Contains("HalfDay", StringComparison.OrdinalIgnoreCase) 
                      || notes.Contains("Half Day", StringComparison.OrdinalIgnoreCase)
                      || notes.Contains("Partial Leave", StringComparison.OrdinalIgnoreCase)
                      || (record.HoursWorked > 0 && ((record.PaidLeaveHours ?? 0) > 0 || (record.UnpaidLeaveHours ?? 0) > 0 || record.Status == AttendanceStatus.LeaveAuthorized || record.Status == AttendanceStatus.Sick))
                      || (record.PaidLeaveHours.HasValue && record.PaidLeaveHours.Value > 0 && record.PaidLeaveHours.Value < dailyHours * 0.9)
                      || (record.UnpaidLeaveHours.HasValue && record.UnpaidLeaveHours.Value > 0 && record.UnpaidLeaveHours.Value < dailyHours * 0.9))
                {
                    periodModifier = " - Half Day";
                    daysCount = 0.5;
                }
            }

            return ($"{baseCategory}{periodModifier}", daysCount);
        }
    }
}
