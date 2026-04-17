using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IEmployeeImportService
    {
        Task<(int SuccessCount, int FailureCount, List<string> Errors)> ImportEmployeesAsync(Stream csvStream);
    }
}
