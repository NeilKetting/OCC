using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface ISupplierService
    {
        Task<IEnumerable<SupplierSummaryDto>> GetSupplierSummariesAsync();
        Task<IEnumerable<Supplier>> GetSuppliersAsync();
        Task<Supplier?> GetSupplierAsync(Guid id);
        Task<Supplier> CreateSupplierAsync(Supplier supplier);
        Task UpdateSupplierAsync(Supplier supplier);
        Task DeleteSupplierAsync(Guid id);
    }
}
