using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IProjectVariationOrderService
    {
        Task<IEnumerable<ProjectVariationOrder>> GetVariationOrdersAsync(Guid? projectId = null);
        Task<ProjectVariationOrder> GetVariationOrderAsync(Guid id);
        Task<ProjectVariationOrder> CreateVariationOrderAsync(ProjectVariationOrder variationOrder);
        Task UpdateVariationOrderAsync(ProjectVariationOrder variationOrder);
        Task DeleteVariationOrderAsync(Guid id);
    }
}
