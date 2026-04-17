using OCC.Shared.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IInventoryImportService
    {
        Task<(List<InventoryItem> Items, int FailureCount, List<string> Errors)> ImportInventoryAsync(Stream csvStream);
    }
}
