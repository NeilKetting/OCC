using System;

namespace OCC.Shared.Models
{
    public enum LoanType
    {
        CashAdvance,
        Equipment,
        Personal,
        Other
    }

    /// <summary>
    /// Represents a financial loan or advance given to an employee.
    /// Deductions are automatically applied to subsequent Wage Runs.
    /// </summary>
    public class EmployeeLoan : BaseEntity
    {
        public Guid EmployeeId { get; set; }
        public virtual Employee? Employee { get; set; }

        public decimal PrincipalAmount { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public decimal OutstandingBalance { get; set; }
        
        /// <summary> Interest rate locked at the time of loan creation (percentage). </summary>
        public decimal InterestRate { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public LoanType LoanType { get; set; } = LoanType.CashAdvance;
        
        public new bool IsActive { get; set; } = true;
        public string Notes { get; set; } = string.Empty;
    }
}
