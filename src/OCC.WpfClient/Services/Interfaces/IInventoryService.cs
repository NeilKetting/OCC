using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IInventoryService
    {
        Task<IEnumerable<InventoryItem>> GetInventoryAsync();
        Task<InventoryItem?> GetInventoryItemAsync(Guid id);
        Task<InventoryItem> CreateItemAsync(InventoryItem item);
        Task UpdateItemAsync(InventoryItem item);
        Task DeleteItemAsync(Guid id);
    }
}
