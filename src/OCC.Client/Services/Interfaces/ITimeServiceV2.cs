using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface ITimeServiceV2
    {
        Task<ClockingEvent?> ClockInAsync(Guid employeeId, DateTime? timestamp = null, string source = "WebPortal");
        Task<ClockingEvent?> ClockOutAsync(Guid employeeId, DateTime? timestamp = null, string source = "WebPortal");
        
        Task<IEnumerable<ClockingEvent>> GetActivePhysicalPresenceAsync();
        
        Task<IEnumerable<DailyTimesheet>> GetDailyTimesheetsAsync(DateTime date);
        Task<IEnumerable<DailyTimesheet>> GetTimesheetsByRangeAsync(DateTime startDate, DateTime endDate);
        
        Task<(bool Success, string Message, int Count)> RepairSyncV2Async();
    }
}
