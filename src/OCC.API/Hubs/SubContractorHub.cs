using Microsoft.AspNetCore.SignalR;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.API.Hubs
{
    /// <summary>
    /// Feature-specific SignalR Hub methods for Sub-Contractors & Snag Jobs real-time delta payload streaming.
    /// </summary>
    public partial class NotificationHub
    {
        public async Task SendSubContractorChanged(EntityChangeDto<SubContractorSummaryDto> change)
        {
            await Clients.All.SendAsync("SubContractorChanged", change);
        }

        public async Task SendSnagJobChanged(EntityChangeDto<SnagJob> change)
        {
            await Clients.All.SendAsync("SnagJobChanged", change);
        }
    }
}
