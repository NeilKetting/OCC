using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IOrderService
    {
        Task<IEnumerable<Order>> GetOrdersAsync();
        Task<Order?> GetOrderAsync(Guid id);
        Task<Order> CreateOrderAsync(Order order);
        Task UpdateOrderAsync(Order order);
        Task DeleteOrderAsync(Guid id);
        Task<Order> CreateNewOrderTemplateAsync(OrderType type);
        Task<Order?> ReceiveOrderAsync(Guid orderId, List<OrderLine> updatedLines);
    }
}
