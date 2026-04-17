using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a single employee's wage calculation within a specific <see cref="WageRun"/>.
    /// Captures the breakdown of hours, rates, and final payment amount.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>WageRunLines</c> table.
    /// <b>How:</b> Calculated based on <see cref="AttendanceRecord"/> data for the period. 
    /// Includes logic for handling "Projected Hours" (advance payment) and correcting variances from previous runs.
    /// </remarks>
    public class WageRunLine : BaseEntity
    {

        
        /// <summary> Foreign Key to the parent <see cref="WageRun"/>. </summary>
        public Guid WageRunId { get; set; }
        
        /// <summary> Foreign Key to the <see cref="Employee"/> being paid. </summary>
        public Guid EmployeeId { get; set; }
        
        /// <summary> Snapshot of the employee's name at time of generation. </summary>
        public string EmployeeName { get; set; } = string.Empty;

        /// <summary> Snapshot of the employee's unique number. </summary>
        public string EmployeeNumber { get; set; } = string.Empty;

        /// <summary> The branch where the employee is based. </summary>
        public string Branch { get; set; } = string.Empty;
        
        /// <summary> Snapshot of the employee's bank name. </summary>
        public string? BankName { get; set; }

        /// <summary> Snapshot of employment type (Permanent/Contract). </summary>
        public string EmploymentType { get; set; } = string.Empty;

        /// <summary> Total standard working hours verified for this period. </summary>
        public double NormalHours { get; set; }

        /// <summary> Total authorized overtime hours (Saturday or Weekday Late @ 1.5x). </summary>
        public double Overtime15Hours { get; set; }
        
        /// <summary> Total authorized overtime hours (Sunday or Public Holiday @ 2.0x). </summary>
        public double Overtime20Hours { get; set; }
        
        /// <summary> 
        /// Hours estimated for future work within this pay cycle (used for Advance Payments).
        /// e.g., Paying for Friday on a Thursday run.
        /// </summary>
        public double ProjectedHours { get; set; } 
        
        /// <summary> 
        /// Correction hours from previous periods.
        /// e.g., If an employee was paid for 9 projected hours last week but was absent, this will be negative to recoup costs.
        /// </summary>
        public double VarianceHours { get; set; } 

        /// <summary> Total unpaid lunch hours deducted during the period (12:00-13:00 slot). </summary>
        public double LunchDeductionHours { get; set; } 
        
        /// <summary> Explanation for any <see cref="VarianceHours"/> applied. </summary>
        public string VarianceNotes { get; set; } = string.Empty;

        /// <summary> The hourly pay rate applied (Snapshot). </summary>
        public decimal HourlyRate { get; set; }

        /// <summary> The final calculated wage amount (Hours * Rate). </summary>
        public decimal TotalWage { get; set; }

        /// <summary> Amount deducted for loan repayments. </summary>
        public decimal DeductionLoan { get; set; }

        /// <summary> Amount deducted for tax/PAYE/UIF. </summary>
        public decimal DeductionTax { get; set; }

        /// <summary> Amount deducted for washing. </summary>
        public decimal DeductionWashing { get; set; }

        /// <summary> Amount deducted for gas (company housing). </summary>
        public decimal DeductionGas { get; set; }

        /// <summary> Other deductions (e.g., damages). </summary>
        public decimal DeductionOther { get; set; }

        /// <summary> Deduction for PPE (Personal Protective Equipment). </summary>
        public decimal DeductionPPE { get; set; }

        /// <summary> Supervisor incentive fee (e.g., R500). </summary>
        public decimal IncentiveSupervisor { get; set; }

        /// <summary> 
        /// Final payout amount: TotalWage + Incentives - Deductions. 
        /// </summary>
        public decimal NetPay => (TotalWage + IncentiveSupervisor) - (DeductionLoan + DeductionTax + DeductionWashing + DeductionGas + DeductionOther + DeductionPPE);

        /// <summary> Snapshot of worked days in the first week of the cycle. </summary>
        public double DaysWorkedWeek1 { get; set; }
        
        /// <summary> Snapshot of worked days in the second week of the cycle. </summary>
        public double DaysWorkedWeek2 { get; set; }
        
        /// <summary> Total days worked during the period. </summary>
        public double TotalDaysWorked { get; set; }

        /// <summary> Flag indicating if the employee lives in company housing. </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsCompanyHoused { get; set; }

        /// <summary> Flag indicating if the employee is in a supervisor role. </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsSupervisor { get; set; }

        // IEntity Implementation - Replaced by BaseEntity

    }
}
