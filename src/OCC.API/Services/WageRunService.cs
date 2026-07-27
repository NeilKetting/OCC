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
            // Request contains StartDate, EndDate. RunDate is Now.
            var runDate = DateTime.Now.Date; // "Today"
            var branchEnum = request.Branch.ToBranchEnum();
            bool isCapeTown = branchEnum == Branch.CPT;

            // 1. DUPLICATION CHECK: Prevent generating if a FINALIZED run already exists
            var existingRun = await _context.WageRuns
                .AnyAsync(w => w.StartDate == request.StartDate && 
                               w.EndDate == request.EndDate && 
                               w.Branch == request.Branch && 
                               w.PayType == request.PayType &&
                               (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid));
            
            if (existingRun)
            {
                throw new ArgumentException($"A Finalized Wage Run already exists for {request.Branch} ({request.PayType}) between {request.StartDate:yyyy-MM-dd} and {request.EndDate:yyyy-MM-dd}. Please delete the finalized run first if you need to regenerate.");
            }
            
            // 2. Create the Shell
            var draftRun = new WageRun
            {
                Id = Guid.NewGuid(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                RunDate = runDate,
                Status = WageRunStatus.Draft,
                PayType = request.PayType,
                Branch = request.Branch, // Ensure Branch is set from request
                Notes = request.Notes
            };

            // 2. Fetch Active Staff matching the PayType
            var rateType = RateType.Hourly; // Default
            if (!string.IsNullOrEmpty(request.PayType) && Enum.TryParse<RateType>(request.PayType, out var parsedType))
            {
                rateType = parsedType;
            }

            var employeesQuery = _context.Employees
                .Where(e => e.Status == EmployeeStatus.Active && e.RateType == rateType);
                
            if (!string.IsNullOrEmpty(request.Branch) && request.Branch != "All")
            {
                employeesQuery = employeesQuery.Where(e => e.Branch == request.Branch);
            }

            var employees = await employeesQuery.ToListAsync();

            // Load BIBC Rate from settings
            decimal bibcRate = 28.75m;
            var bibcSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "BibcRate");
            if (bibcSetting != null && decimal.TryParse(bibcSetting.Value, out var parsedRate))
            {
                bibcRate = parsedRate;
            }
            
            // Calculate gas split
            var housedEmployees = employees.Where(e => e.LivesInCompanyHousing).ToList();
            decimal gasPerPerson = 0;
            if (housedEmployees.Count > 0 && request.InputTotalGasCharge > 0)
            {
                gasPerPerson = request.InputTotalGasCharge / housedEmployees.Count;
            }

            // 3. Load CompanyProfile to resolve branch shift defaults (no hardcoded times)
            CompanyDetails? companyDetails = null;
            var profileSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "CompanyProfile");
            if (profileSetting != null && !string.IsNullOrEmpty(profileSetting.Value))
            {
                companyDetails = JsonSerializer.Deserialize<CompanyDetails>(profileSetting.Value);
            }

            // 4. Fetch Attendance for the Period (up to EndDate of Week 2) and any historical unpaid paid/leave records
            var attendance = await _context.AttendanceRecords
                .Where(a => a.PaidWageRunId == null && 
                            ((a.Date >= request.StartDate && a.Date <= request.EndDate) ||
                             (a.Date < request.StartDate && 
                              (a.Status == AttendanceStatus.Sick || a.Status == AttendanceStatus.LeaveAuthorized || (a.PaidLeaveHours != null && a.PaidLeaveHours > 0)))))
                .ToListAsync();

            // 4. Fetch Active Loans
            var activeLoans = await _context.EmployeeLoans
                .Where(l => l.IsActive && l.OutstandingBalance > 0 && l.StartDate <= runDate)
                .ToListAsync();

            // 4. Fetch Previous Finalized Run (for Variance) - MUST BE BRANCH SPECIFIC
            var lastRun = await _context.WageRuns
                .Include(w => w.Lines)
                .Where(w => (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid) && 
                            (w.Branch == request.Branch || w.Branch == "All" ||
                             (request.Branch == "Johannesburg" && (w.Branch == "JHB" || w.Branch == "Johannesburg")) ||
                             (request.Branch == "Cape Town" && (w.Branch == "CPT" || w.Branch == "Cape Town"))) && 
                            w.PayType == request.PayType &&
                            w.EndDate < request.StartDate)
                .OrderByDescending(w => w.EndDate)
                .FirstOrDefaultAsync();

            // 5. Pre-load prepaid window (Thursday/Friday of previous cycle) leave requests and attendance records
            var prepaidStart = request.StartDate.AddDays(-2).Date;
            var prepaidEnd = request.StartDate.AddDays(-1).Date;

            var activeLeaveRequests = await _context.LeaveRequests
                .Where(lr => lr.Status == LeaveStatus.Approved &&
                             lr.StartDate <= request.EndDate.AddDays(2) &&
                             lr.EndDate >= prepaidStart.AddDays(-2))
                .ToListAsync();

            var prepaidAttendanceRecords = await _context.AttendanceRecords
                .Where(ar => ar.Date >= prepaidStart &&
                             ar.Date <= prepaidEnd)
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
                    DeductionGas = 0, // Initialized to 0, set below
                    DeductionWashing = 0, // Initialized to 0, set below
                    IncentiveSupervisor = 0, // Initialized to 0, set below
                    IsCompanyHoused = emp.LivesInCompanyHousing,
                    IsSupervisor = (emp.Role == EmployeeRole.Supervisor || emp.Role == EmployeeRole.SiteManager ||
                                    emp.Role == EmployeeRole.BuildingSupervisor || emp.Role == EmployeeRole.PlasterSupervisor ||
                                    emp.Role == EmployeeRole.ShopfittingSupervisor || emp.Role == EmployeeRole.PaintingSupervisor ||
                                    emp.Role == EmployeeRole.LabourSupervisor),
                    IsBibc = emp.IsBibc
                };

                // Supervisor Incentive: try to reuse the fee from the previous finalized run first
                decimal supervisorFee = 0;
                if (lastRun != null)
                {
                    var lastLine = lastRun.Lines.FirstOrDefault(l => l.EmployeeId == emp.Id);
                    if (lastLine != null && lastLine.IncentiveSupervisor > 0)
                    {
                        supervisorFee = lastLine.IncentiveSupervisor;
                    }
                }

                // If not found in the previous run, fall back to default fee if they are in a supervisor role
                if (supervisorFee == 0 &&
                    (emp.Role == EmployeeRole.Supervisor || emp.Role == EmployeeRole.SiteManager ||
                     emp.Role == EmployeeRole.BuildingSupervisor || emp.Role == EmployeeRole.PlasterSupervisor ||
                     emp.Role == EmployeeRole.ShopfittingSupervisor || emp.Role == EmployeeRole.PaintingSupervisor ||
                     emp.Role == EmployeeRole.LabourSupervisor))
                {
                    supervisorFee = request.InputDefaultSupervisorFee;
                }

                line.IncentiveSupervisor = supervisorFee;

                // Gas and specific washing deduction for Company Housing
                if (emp.LivesInCompanyHousing)
                {
                    line.DeductionGas = gasPerPerson;
                    if (request.InputCompanyHousingWashingFee > 0)
                    {
                        line.DeductionWashing = request.InputCompanyHousingWashingFee;
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
                
                // Track distinct dates worked in each week
                var distinctDaysW1 = new HashSet<DateTime>();
                var distinctDaysW2 = new HashSet<DateTime>();

                // Resolve branch shift defaults for this employee if no personal shift is set
                var empForCalc = emp;
                if ((emp.ShiftStartTime == null || emp.ShiftEndTime == null) && companyDetails != null)
                {
                    // Map the employee's branch string to the Branch enum key
                    var branchKey = emp.Branch?.Contains("Cape", StringComparison.OrdinalIgnoreCase) == true
                        ? Branch.CPT
                        : Branch.JHB;

                    if (companyDetails?.Branches != null && companyDetails.Branches.TryGetValue(branchKey, out var branchDetails))
                    {
                        // Clone with the branch shift so the original DB entity is untouched
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

                // Calculate standard weekday shift duration for the employee
                double dailyHours = 9.0;
                if (empForCalc.ShiftStartTime.HasValue && empForCalc.ShiftEndTime.HasValue)
                {
                    dailyHours = (empForCalc.ShiftEndTime.Value - empForCalc.ShiftStartTime.Value).TotalHours;
                    if (empForCalc.ShiftEndTime.Value.Hours >= 13)
                    {
                        dailyHours -= 1.0;
                    }
                    if (dailyHours < 0) dailyHours = 0;
                }

                foreach (var record in empAttendance)
                {
                    var hours = _wageCalc.CalculateHours(record, empForCalc);
                    if (record.Date >= request.StartDate)
                    {
                        line.NormalHours         += hours.Normal;
                        if (record.Date.DayOfWeek == DayOfWeek.Saturday)
                        {
                            line.SaturdayOvertimeHours += hours.Overtime15;
                        }
                        else
                        {
                            line.Overtime15Hours     += hours.Overtime15;
                        }
                        line.Overtime20Hours     += hours.Overtime20;
                        line.LunchDeductionHours += hours.Lunch;
                        
                        if (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late || record.Status == AttendanceStatus.LeaveEarly)
                        {
                            var hasUnpaidLeave = empLeaveRequests.Any(lr => IsUnpaidLeaveForDate(lr, record.Date));

                            if (!hasUnpaidLeave)
                            {
                                bool isWeekend = record.Date.DayOfWeek == DayOfWeek.Saturday || record.Date.DayOfWeek == DayOfWeek.Sunday;
                                bool isEmpCapeTown = emp.Branch?.Contains("Cape", StringComparison.OrdinalIgnoreCase) == true;
                                if (!isEmpCapeTown || !isWeekend)
                                {
                                    if (record.Date.Date <= week1End.Date) distinctDaysW1.Add(record.Date.Date);
                                    else distinctDaysW2.Add(record.Date.Date);
                                }
                            }
                        }

                        if (record.Status == AttendanceStatus.Absent)
                        {
                            line.VarianceNotes += $"{record.Date:dd/MM}: Absent; ";
                        }
                        else if (record.Status == AttendanceStatus.Sick)
                        {
                            line.VarianceNotes += $"{record.Date:dd/MM}: Sick; ";
                        }
                        else if (record.Status == AttendanceStatus.LeaveAuthorized)
                        {
                            line.VarianceNotes += $"{record.Date:dd/MM}: Leave; ";
                        }
                        else if (record.Status == AttendanceStatus.UnpaidSick)
                        {
                            line.VarianceNotes += $"{record.Date:dd/MM}: Unpaid Sick; ";
                        }
                        else if (record.Status == AttendanceStatus.UnpaidLeave)
                        {
                            line.VarianceNotes += $"{record.Date:dd/MM}: Unpaid Leave; ";
                        }
                        else if (record.Status == AttendanceStatus.UnpaidHalfDay)
                        {
                            line.VarianceNotes += $"{record.Date:dd/MM}: Unpaid Half Day; ";
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

                line.DaysWorkedWeek1 = 0; // W1 (deducted days offset)
                line.DaysWorkedWeek2 = distinctDaysW1.Count; // W2 (Week 1 actual worked)
                line.DaysWorkedWeek3 = distinctDaysW2.Count; // W3 (Week 2 actual worked)
                line.TotalDaysWorked = distinctDaysW1.Count + distinctDaysW2.Count;

                // B. Calculate Projected Hours (Thursday to Friday of the run period)
                var projectedStart = DateTime.MinValue;
                var projectedEnd = DateTime.MinValue;
                int startOffset = isCapeTown ? 0 : 7;
                int endOffset = isCapeTown ? 6 : 13;
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
                        // Skip Weekend or Public Holiday
                        if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d)) continue;

                        // Check if there is an approved unpaid/absent leave request for this projected day
                        var hasUnpaidProjLeave = empLeaveRequests.Any(lr => IsUnpaidLeaveForDate(lr, d));

                        if (hasUnpaidProjLeave) continue;

                        // Check if there is an attendance record for this day
                        var record = empAttendance.FirstOrDefault(r => r.Date.Date == d.Date);
                        if (record == null)
                        {
                            // If there isn't any record, we by default pay them
                            line.ProjectedHours += dailyHours;
                        }
                    }
                }

                int projectedDays = dailyHours > 0 ? (int)Math.Round(line.ProjectedHours / dailyHours) : 0;
                if (isCapeTown)
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

                // C. Variance Calculation (Previous Run)
                double leaveDeductionDays = 0;
                double leaveDeductionHours = 0;
                int prepaidWorkingDays = 0;

                var empAttendanceRecords = prepaidAttendanceRecords
                    .Where(ar => ar.EmployeeId == emp.Id)
                    .ToList();

                // Find the previous run line to determine what projected hours were actually paid in advance
                var priorLine = lastRun?.Lines?.FirstOrDefault(l => l.EmployeeId == emp.Id);

                // Use shift dailyHours if available, default to 8.75
                double standardDayHours = dailyHours > 0 ? dailyHours : 8.75;

                // Allow max projected hours to deduct based on prior line if available (>0), or default to max 2 prepaid days
                double maxProjectedHoursToDeduct = (priorLine != null && priorLine.ProjectedHours > 0)
                    ? (double)priorLine.ProjectedHours
                    : (2.0 * standardDayHours);

                double remainingProjectedHours = maxProjectedHoursToDeduct;
                var absentDatesList = new List<string>();

                for (var d = prepaidEnd; d >= prepaidStart; d = d.AddDays(-1))
                {
                    // Skip weekends and public holidays
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

                    // Only deduct if this day was paid as projected in the previous run
                    if (remainingProjectedHours >= dayHours - 0.01)
                    {
                        remainingProjectedHours -= dayHours;

                        var isAbsent = empLeaveRequests.Any(lr => IsUnpaidLeaveForDate(lr, d));

                        if (!isAbsent)
                        {
                            var attendanceRecord = empAttendanceRecords.FirstOrDefault(ar =>
                                ar.Date.Date == d.Date ||
                                (ar.Date.Kind == DateTimeKind.Utc ? ar.Date.ToLocalTime().Date : ar.Date.Date) == d.Date);

                            if (attendanceRecord != null && (attendanceRecord.Status == AttendanceStatus.Absent || attendanceRecord.Status == AttendanceStatus.UnpaidSick || attendanceRecord.Status == AttendanceStatus.UnpaidLeave))
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

                // Recalculate TotalDaysWorked to include the deducted days offset (Week 1)
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

                // D. Total Wage
                var calculatedWage = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours) * line.HourlyRate +
                                     (decimal)(line.Overtime15Hours + line.SaturdayOvertimeHours) * line.HourlyRate * 1.5m +
                                     (decimal)line.Overtime20Hours * line.HourlyRate * 2.0m;
                line.TotalWage = Math.Max(0m, calculatedWage);
                    
                // E. Loans (deducted according to frequency specified in loan agreement)
                var empLoans = activeLoans.Where(l => l.EmployeeId == emp.Id).ToList();
                decimal totalLoanDeduction = 0;
                foreach (var loan in empLoans)
                {
                    string frequency = "";
                    if (!string.IsNullOrEmpty(loan.Notes) && loan.Notes.StartsWith("[Term:") && loan.Notes.Contains("]"))
                    {
                        int termEnd = loan.Notes.IndexOf(']');
                        string header = loan.Notes.Substring(1, termEnd - 1);
                        string[] parts = header.Split(',');
                        foreach (var part in parts)
                        {
                            if (part.Contains("Term:"))
                            {
                                frequency = part.Replace("Term:", "").Trim();
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(frequency))
                    {
                        frequency = emp.RateType == RateType.Hourly ? "Fortnightly" : "Monthly";
                    }

                    bool matchesRun = (rateType == RateType.Hourly && frequency == "Fortnightly") ||
                                      (rateType == RateType.MonthlySalary && frequency == "Monthly");

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
                existing.InputTotalGasCharge = run.InputTotalGasCharge;
                existing.InputDefaultSupervisorFee = run.InputDefaultSupervisorFee;
                existing.InputCompanyHousingWashingFee = run.InputCompanyHousingWashingFee;

                _context.WageRunLines.RemoveRange(existing.Lines);
                existing.Lines = run.Lines;
                foreach (var line in existing.Lines)
                {
                    if (line.Id == Guid.Empty) line.Id = Guid.NewGuid();
                    line.WageRunId = existing.Id;
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
                    var rateType = RateType.Hourly; // Default
                    if (!string.IsNullOrEmpty(run.PayType) && Enum.TryParse<RateType>(run.PayType, out var parsedType))
                    {
                        rateType = parsedType;
                    }

                    var activeLoans = await _context.EmployeeLoans
                        .Where(l => l.EmployeeId == line.EmployeeId && l.IsActive && l.OutstandingBalance > 0)
                        .OrderBy(l => l.StartDate)
                        .ToListAsync();

                    var matchingLoans = new List<EmployeeLoan>();
                    foreach (var loan in activeLoans)
                    {
                        string frequency = "";
                        if (!string.IsNullOrEmpty(loan.Notes) && loan.Notes.StartsWith("[Term:") && loan.Notes.Contains("]"))
                        {
                            int termEnd = loan.Notes.IndexOf(']');
                            string header = loan.Notes.Substring(1, termEnd - 1);
                            string[] parts = header.Split(',');
                            foreach (var part in parts)
                            {
                                if (part.Contains("Term:"))
                                {
                                    frequency = part.Replace("Term:", "").Trim();
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(frequency))
                        {
                            var employee = await _context.Employees.FindAsync(loan.EmployeeId);
                            frequency = employee?.RateType == RateType.Hourly ? "Fortnightly" : "Monthly";
                        }

                        bool matchesRun = (rateType == RateType.Hourly && frequency == "Fortnightly") ||
                                          (rateType == RateType.MonthlySalary && frequency == "Monthly");

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

            // Mark all processed attendance records as paid by this wage run
            var runDateFinal = run.RunDate != default ? run.RunDate : DateTime.Now.Date;
            var cutoffDateFinal = DateTime.MinValue;
            int startOffset = run.Branch == "Cape Town" ? 0 : 7;
            int endOffset = run.Branch == "Cape Town" ? 6 : 13;
            for (int i = startOffset; i <= endOffset; i++)
            {
                var date = run.StartDate.AddDays(i).Date;
                if (date.DayOfWeek == DayOfWeek.Wednesday)
                {
                    cutoffDateFinal = date;
                    break;
                }
            }
            var attendanceEndFinal = cutoffDateFinal > runDateFinal ? runDateFinal : cutoffDateFinal;

            var employeeIds = run.Lines.Select(l => l.EmployeeId).ToList();

            var attendanceRecordsToFinalize = await _context.AttendanceRecords
                .Where(a => a.PaidWageRunId == null && 
                            a.EmployeeId != null &&
                            employeeIds.Contains(a.EmployeeId.Value) &&
                            ((a.Date >= run.StartDate && a.Date <= attendanceEndFinal) ||
                             (a.Date < run.StartDate && 
                              (a.Status == AttendanceStatus.Sick || a.Status == AttendanceStatus.LeaveAuthorized || (a.PaidLeaveHours != null && a.PaidLeaveHours > 0)))))
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

            // Normalize lr.StartDate and lr.EndDate handling potential UTC stored values
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
    }
}
