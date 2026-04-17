using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface ICustomerService
    {
        Task<IEnumerable<CustomerSummaryDto>> GetCustomerSummariesAsync();
        Task<Customer?> GetCustomerAsync(Guid id);
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<bool> UpdateCustomerAsync(Customer customer);
        Task<bool> DeleteCustomerAsync(Guid id);
    }
}
