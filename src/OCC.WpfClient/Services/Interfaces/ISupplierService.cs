using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ISupplierService
    {
        Task<IEnumerable<SupplierSummaryDto>> GetSupplierSummariesAsync();
        Task<IEnumerable<Supplier>> GetSuppliersAsync();
        Task<Supplier?> GetSupplierAsync(System.Guid id);
        Task<Supplier> CreateSupplierAsync(Supplier supplier);
        Task UpdateSupplierAsync(Supplier supplier);
        Task DeleteSupplierAsync(System.Guid id);
    }
}
