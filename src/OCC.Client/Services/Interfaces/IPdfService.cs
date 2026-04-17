using OCC.Shared.Models;
using System.Threading.Tasks;
using System;

namespace OCC.Client.Services.Interfaces
{
    public interface IPdfService
    {
        Task<string> GenerateOrderPdfAsync(Order order, bool isPrintVersion = false);
        Task<string> GenerateEmployeeReportPdfAsync<T>(Employee employee, DateTime start, DateTime end, System.Collections.Generic.IEnumerable<T> data, System.Collections.Generic.Dictionary<string, string> summary);
        Task<string> GenerateLoanSchedulePdfAsync(EmployeeLoan loan, Employee employee);
        Task<string> GenerateWageRunPdfAsync(WageRun wageRun);
    }
}
