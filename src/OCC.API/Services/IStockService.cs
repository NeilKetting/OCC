using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    public interface IStockService
    {
        Task AdjustStockAsync(Guid itemId, double quantityChange, Branch branch);
    }
}
