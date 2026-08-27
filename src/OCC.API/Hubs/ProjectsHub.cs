using Microsoft.AspNetCore.SignalR;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.API.Hubs
{
    /// <summary>
    /// Feature-specific SignalR Hub methods for Projects real-time delta payload streaming.
    /// </summary>
    public partial class NotificationHub
    {
        public async Task SendProjectChanged(EntityChangeDto<ProjectSummaryDto> change)
        {
            await Clients.All.SendAsync("ProjectChanged", change);
        }

        public async Task SendProjectTaskChanged(EntityChangeDto<ProjectTask> change)
        {
            await Clients.All.SendAsync("ProjectTaskChanged", change);
        }
    }
}
