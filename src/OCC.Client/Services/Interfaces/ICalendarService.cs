using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Client.Features.CalendarHub.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface ICalendarService
    {
        Task<List<CalendarEvent>> GetEventsAsync(DateTime start, DateTime end);
    }
}
