using System;
using System.Collections.Generic;

namespace OCC.Shared.DTOs
{
    public class LoanStatementPaymentDto
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public decimal BalanceAfterPayment { get; set; }
    }

    public class LoanStatementDto
    {
        public Guid LoanId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public decimal PrincipalAmount { get; set; }
        public decimal InterestRate { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public decimal OutstandingBalance { get; set; }
        public DateTime StartDate { get; set; }
        public List<LoanStatementPaymentDto> Payments { get; set; } = new();
    }
}
