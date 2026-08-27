using Microsoft.AspNetCore.SignalR;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.API.Hubs
{
    /// <summary>
    /// Feature-specific SignalR Hub methods for Customers real-time delta payload streaming.
    /// </summary>
    public partial class NotificationHub
    {
        public async Task SendCustomerChanged(EntityChangeDto<CustomerSummaryDto> change)
        {
            await Clients.All.SendAsync("CustomerChanged", change);
        }
    }
}
