using OCC.Shared.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface ISupplierImportService
    {
        Task<(int SuccessCount, int FailureCount, List<string> Errors)> ImportSuppliersAsync(Stream csvStream);
    }
}
