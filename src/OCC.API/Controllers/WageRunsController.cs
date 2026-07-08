using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WageRunsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWageCalculationService _wageCalc;

        public WageRunsController(AppDbContext context, IWageCalculationService wageCalc)
        {
            _context  = context;
            _wageCalc = wageCalc;
        }

        // GET: api/WageRuns
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WageRun>>> GetWageRuns()
        {
            return await _context.WageRuns
                .Include(w => w.Lines)
                .OrderByDescending(w => w.StartDate)
                .ToListAsync();
        }

        // GET: api/WageRuns/5
        [HttpGet("{id}")]
        public async Task<ActionResult<WageRun>> GetWageRun(Guid id)
        {
            var wageRun = await _context.WageRuns
                .Include(w => w.Lines)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (wageRun == null)
            {
                return NotFound();
            }

            return wageRun;
        }

        // POST: api/WageRuns/draft
        [HttpPost("draft")]
        public async Task<ActionResult<WageRun>> GenerateDraft([FromBody] WageRun request)
        {
            // Request contains StartDate, EndDate. RunDate is Now.
            var runDate = DateTime.Now.Date; // "Today"

            // 1. DUPLICATION CHECK: Prevent generating if a FINALIZED run already exists
            var existingRun = await _context.WageRuns
                .AnyAsync(w => w.StartDate == request.StartDate && 
                               w.EndDate == request.EndDate && 
                               w.Branch == request.Branch && 
                               w.PayType == request.PayType &&
                               (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid));
            
            if (existingRun)
            {
                return BadRequest($"A Finalized Wage Run already exists for {request.Branch} ({request.PayType}) between {request.StartDate:yyyy-MM-dd} and {request.EndDate:yyyy-MM-dd}. Please delete the finalized run first if you need to regenerate.");
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

            // 4. Fetch Attendance for the Period (up to Wednesday of Week 2)
            var cutoffDate = request.StartDate.AddDays(9).Date;
            var attendanceEnd = cutoffDate > runDate ? runDate : cutoffDate;
            var attendance = await _context.AttendanceRecords
                .Where(a => a.Date >= request.StartDate && a.Date <= attendanceEnd)
                .ToListAsync();

            // 4. Fetch Active Loans
            var activeLoans = await _context.EmployeeLoans
                .Where(l => l.IsActive && l.OutstandingBalance > 0 && l.StartDate <= runDate)
                .ToListAsync();

            // 4. Fetch Previous Finalized Run (for Variance) - MUST BE BRANCH SPECIFIC
            var lastRun = await _context.WageRuns
                .Include(w => w.Lines)
                .Where(w => (w.Status == WageRunStatus.Finalized || w.Status == WageRunStatus.Paid) && 
                            w.Branch == request.Branch && 
                            w.PayType == request.PayType &&
                            w.EndDate < request.StartDate)
                .OrderByDescending(w => w.EndDate)
                .FirstOrDefaultAsync();

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
                                    emp.Role == EmployeeRole.LabourSupervisor)
                };

                // Default Supervisor Incentive
                if (emp.Role == EmployeeRole.Supervisor || emp.Role == EmployeeRole.SiteManager ||
                    emp.Role == EmployeeRole.BuildingSupervisor || emp.Role == EmployeeRole.PlasterSupervisor ||
                    emp.Role == EmployeeRole.ShopfittingSupervisor || emp.Role == EmployeeRole.PaintingSupervisor ||
                    emp.Role == EmployeeRole.LabourSupervisor)
                {
                    line.IncentiveSupervisor = request.InputDefaultSupervisorFee;
                }

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

                    if (companyDetails.Branches.TryGetValue(branchKey, out var branchDetails))
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
                    line.NormalHours         += hours.Normal;
                    line.Overtime15Hours     += hours.Overtime15;
                    line.Overtime20Hours     += hours.Overtime20;
                    line.LunchDeductionHours += hours.Lunch;
                    
                    if (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late || record.Status == AttendanceStatus.LeaveEarly)
                    {
                        if (record.Date.Date <= week1End.Date) distinctDaysW1.Add(record.Date.Date);
                        else distinctDaysW2.Add(record.Date.Date);
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
                }
                line.DaysWorkedWeek1 = 0; // W1 (deducted days offset)
                line.DaysWorkedWeek2 = distinctDaysW1.Count; // W2 (Week 1 actual worked)
                line.DaysWorkedWeek3 = distinctDaysW2.Count; // W3 (Week 2 actual worked)
                line.TotalDaysWorked = distinctDaysW1.Count + distinctDaysW2.Count;

                // B. Calculate Projected Hours (Thursday Week 2 to Friday Week 2)
                var projectedStart = request.StartDate.AddDays(10).Date;
                var projectedEnd = request.StartDate.AddDays(11).Date;

                if (projectedStart <= projectedEnd)
                {
                    for (var d = projectedStart; d <= projectedEnd; d = d.AddDays(1))
                    {
                        var dow = d.DayOfWeek;
                        // Skip Weekend or Public Holiday
                        if (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d)) continue;

                        line.ProjectedHours += dailyHours;
                    }
                }

                // C. Variance Calculation (Previous Run)
                if (lastRun != null)
                {
                    var lastLine = lastRun.Lines.FirstOrDefault(l => l.EmployeeId == emp.Id);
                    if (lastLine != null && lastLine.ProjectedHours > 0)
                    {
                        // Check what ACTUALLY happened in that window by looking at Leave Management (LeaveRequests)
                        // Last Run Projected Window is always Thursday and Friday of Week 2 of that run
                        var lastRunProjectedStart = lastRun.StartDate.AddDays(10).Date;
                        var lastRunProjectedEnd = lastRun.StartDate.AddDays(11).Date;

                        if (lastRunProjectedStart <= lastRunProjectedEnd)
                        {
                            var leaveRequests = await _context.LeaveRequests
                                .Where(lr => lr.EmployeeId == emp.Id &&
                                             lr.Status == LeaveStatus.Approved &&
                                             lr.StartDate <= lastRunProjectedEnd &&
                                             lr.EndDate >= lastRunProjectedStart)
                                .ToListAsync();

                            var attendanceRecords = await _context.AttendanceRecords
                                .Where(ar => ar.EmployeeId == emp.Id &&
                                             ar.Date >= lastRunProjectedStart &&
                                             ar.Date <= lastRunProjectedEnd)
                                .ToListAsync();

                            double leaveDeductionDays = 0;
                            for (var d = lastRunProjectedStart; d <= lastRunProjectedEnd; d = d.AddDays(1))
                            {
                                // Skip weekends and public holidays as they are not standard working days to deduct
                                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday || OCC.Shared.Utils.HolidayUtils.IsPublicHoliday(d))
                                    continue;

                                var isAbsent = leaveRequests.Any(lr => d >= lr.StartDate.Date && d <= lr.EndDate.Date &&
                                    (lr.IsUnpaid || lr.LeaveType == LeaveType.Unpaid || lr.LeaveType == LeaveType.AbsentWithoutLeave));

                                if (!isAbsent)
                                {
                                    var attendanceRecord = attendanceRecords.FirstOrDefault(ar => ar.Date.Date == d);
                                    if (attendanceRecord != null && (attendanceRecord.Status == AttendanceStatus.Absent || attendanceRecord.Status == AttendanceStatus.UnpaidSick))
                                    {
                                        isAbsent = true;
                                    }
                                }

                                if (isAbsent)
                                {
                                    leaveDeductionDays++;
                                }
                            }

                            if (leaveDeductionDays > 0)
                            {
                                line.VarianceHours = -leaveDeductionDays * dailyHours;
                                line.VarianceNotes = $"Adj from {lastRun.EndDate:MMM dd}: Absent {leaveDeductionDays:F1} day(s)";
                                line.DaysWorkedWeek1 = -leaveDeductionDays;
                            }
                        }
                    }
                }

                // Recalculate TotalDaysWorked to include the deducted days offset (Week 1)
                line.TotalDaysWorked = line.DaysWorkedWeek1 + line.DaysWorkedWeek2 + line.DaysWorkedWeek3;

                // D. Total Wage
                // Formula: ((Normal + Projected + Variance) * Rate) + (OT15 * Rate * 1.5) + (OT20 * Rate * 2.0)
                
                line.TotalWage = (decimal)(line.NormalHours + line.ProjectedHours + line.VarianceHours) * line.HourlyRate +
                                 (decimal)line.Overtime15Hours * line.HourlyRate * 1.5m +
                                 (decimal)line.Overtime20Hours * line.HourlyRate * 2.0m;
                    
                // E. Loans (deducted according to frequency specified in loan agreement)
                var empLoans = activeLoans.Where(l => l.EmployeeId == emp.Id).ToList();
                decimal totalLoanDeduction = 0;
                foreach (var loan in empLoans)
                {
                    // Parse frequency from Notes
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

                    // Fallback to legacy matching if not specified
                    if (string.IsNullOrEmpty(frequency))
                    {
                        frequency = emp.RateType == RateType.Hourly ? "Fortnightly" : "Monthly";
                    }

                    // Only deduct if the frequency matches the wage run type
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

            // DO NOT SAVE to DB yet. Return the in-memory calculations for review.
            return Ok(draftRun);
        }

        // POST: api/WageRuns/finalize
        [HttpPost("finalize")]
        public async Task<ActionResult<WageRun>> FinalizeRun([FromBody] WageRun run)
        {
            if (run == null) return BadRequest("Invalid Wage Run data.");

            // Set IDs forLines if missing
            foreach (var line in run.Lines)
            {
                if (line.Id == Guid.Empty) line.Id = Guid.NewGuid();
                line.WageRunId = run.Id;
            }

            run.Status = WageRunStatus.Finalized;
            _context.WageRuns.Add(run);

            // Update employee loan outstanding balances
            foreach (var line in run.Lines)
            {
                if (line.DeductionLoan > 0)
                {
                    // Determine rateType of the finalized wage run
                    var rateType = RateType.Hourly; // Default
                    if (!string.IsNullOrEmpty(run.PayType) && Enum.TryParse<RateType>(run.PayType, out var parsedType))
                    {
                        rateType = parsedType;
                    }

                    // Find active loans for this employee
                    var activeLoans = await _context.EmployeeLoans
                        .Where(l => l.EmployeeId == line.EmployeeId && l.IsActive && l.OutstandingBalance > 0)
                        .OrderBy(l => l.StartDate)
                        .ToListAsync();

                    // Filter loans by frequency matching this wage run
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

            await _context.SaveChangesAsync();
            
            return CreatedAtAction("GetWageRun", new { id = run.Id }, run);
        }

        // PUT: api/WageRuns/draft/{id}/lines
        [HttpPut("draft/{id}/lines")]
        public async Task<IActionResult> UpdateDraftLines(Guid id, [FromBody] List<WageRunLine> updatedLines)
        {
            var run = await _context.WageRuns.Include(w => w.Lines).FirstOrDefaultAsync(w => w.Id == id);
            if (run == null || run.Status != WageRunStatus.Draft) return BadRequest("Run not found or not in Draft state.");

            foreach (var existingLine in run.Lines)
            {
                var update = updatedLines.FirstOrDefault(l => l.Id == existingLine.Id);
                if (update != null)
                {
                    existingLine.DeductionWashing = update.DeductionWashing;
                    existingLine.IncentiveSupervisor = update.IncentiveSupervisor;
                    // DeductionGas is set from the initial total generation, so we won't allow edits here unless requested.
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/WageRuns/delete/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRun(Guid id)
        {
             var run = await _context.WageRuns.FindAsync(id);
             if (run == null) return NotFound();
             
             if (run.Status == WageRunStatus.Finalized) 
                 return BadRequest("Cannot delete a finalized run.");
                 
             _context.WageRuns.Remove(run);
             await _context.SaveChangesAsync();
             return NoContent();
        }

    }
}
