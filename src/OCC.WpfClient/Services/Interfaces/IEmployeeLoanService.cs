using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IEmployeeLoanService
    {
        Task<IEnumerable<EmployeeLoan>> GetAllAsync();
        Task<IEnumerable<EmployeeLoan>> GetActiveLoansAsync();
        Task<EmployeeLoan?> GetByIdAsync(Guid id);
        Task<EmployeeLoan> AddAsync(EmployeeLoan loan);
        Task UpdateAsync(EmployeeLoan loan);
        Task DeleteAsync(Guid id);
        Task<OCC.Shared.DTOs.LoanStatementDto?> GetStatementAsync(Guid loanId);
    }
}
