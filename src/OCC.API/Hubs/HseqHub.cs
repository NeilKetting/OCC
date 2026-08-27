using Microsoft.AspNetCore.SignalR;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.API.Hubs
{
    /// <summary>
    /// Feature-specific SignalR Hub methods for HSEQ (Incidents, Audits, Trainings) real-time delta payload streaming.
    /// </summary>
    public partial class NotificationHub
    {
        public async Task SendIncidentChanged(EntityChangeDto<IncidentSummaryDto> change)
        {
            await Clients.All.SendAsync("IncidentChanged", change);
        }

        public async Task SendAuditChanged(EntityChangeDto<AuditSummaryDto> change)
        {
            await Clients.All.SendAsync("AuditChanged", change);
        }

        public async Task SendTrainingChanged(EntityChangeDto<HseqTrainingSummaryDto> change)
        {
            await Clients.All.SendAsync("TrainingChanged", change);
        }

    }
}
