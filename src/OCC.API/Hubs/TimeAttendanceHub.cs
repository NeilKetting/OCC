using Microsoft.AspNetCore.SignalR;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.API.Hubs
{
    /// <summary>
    /// Feature-specific SignalR Hub methods for Time & Attendance real-time delta payload streaming.
    /// </summary>
    public partial class NotificationHub
    {
        public async Task SendEmployeeChanged(EntityChangeDto<Employee> change)
        {
            await Clients.All.SendAsync("EmployeeChanged", change);
        }

        public async Task SendAttendanceRecordChanged(EntityChangeDto<AttendanceRecord> change)
        {
            await Clients.All.SendAsync("AttendanceRecordChanged", change);
        }

        public async Task SendWageRunChanged(EntityChangeDto<WageRun> change)
        {
            await Clients.All.SendAsync("WageRunChanged", change);
        }

        public async Task SendWageSettingsChanged(EntityChangeDto<WageSettings> change)
        {
            await Clients.All.SendAsync("WageSettingsChanged", change);
        }
    }
}
