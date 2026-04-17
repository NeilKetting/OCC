using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IOrderService
    {
        Task<List<OrderSummaryDto>> GetOrdersAsync();
        Task<OrderDto?> GetOrderAsync(Guid id);
        Task<OrderDto> CreateOrderAsync(OrderDto order);
        Task UpdateOrderAsync(OrderDto order);
        Task<OrderDto?> ReceiveOrderAsync(Guid orderId, List<OrderLineDto> updatedLines);
        Task DeleteOrderAsync(Guid id);
        
        Task<OrderDto> GetRestockTemplateAsync();
        Task<IEnumerable<RestockCandidateDto>> GetRestockCandidatesAsync(Branch? branch = null);
    }
}
