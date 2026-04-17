using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IEmployeeService
    {
        Task<IEnumerable<EmployeeSummaryDto>> GetEmployeesAsync();
        Task<EmployeeDto?> GetEmployeeAsync(Guid id);
        Task<EmployeeDto?> CreateEmployeeAsync(Employee employee);
        Task<bool> UpdateEmployeeAsync(Employee employee);
        Task<bool> DeleteEmployeeAsync(Guid id);
    }
}
