using Microsoft.AspNetCore.SignalR;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.API.Hubs
{
    /// <summary>
    /// Feature-specific SignalR Hub methods for Procurement & Suppliers real-time delta payload streaming.
    /// </summary>
    public partial class NotificationHub
    {
        public async Task SendSupplierChanged(EntityChangeDto<SupplierSummaryDto> change)
        {
            await Clients.All.SendAsync("SupplierChanged", change);
        }

        public async Task SendOrderChanged(EntityChangeDto<Order> change)
        {
            await Clients.All.SendAsync("OrderChanged", change);
        }

    }
}
