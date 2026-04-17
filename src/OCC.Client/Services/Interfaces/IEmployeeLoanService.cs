using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IEmployeeLoanService
    {
        Task<IEnumerable<EmployeeLoan>> GetAllAsync();
        Task<IEnumerable<EmployeeLoan>> GetActiveLoansAsync();
        Task<EmployeeLoan?> GetByIdAsync(Guid id);
        Task AddAsync(EmployeeLoan loan);
        Task UpdateAsync(EmployeeLoan loan);
        Task DeleteAsync(Guid id);
    }
}
