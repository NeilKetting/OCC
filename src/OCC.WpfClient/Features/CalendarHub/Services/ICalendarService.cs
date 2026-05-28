using OCC.WpfClient.Features.CalendarHub.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.CalendarHub.Services
{
    // =========================================================================
    // ICalendarService.cs
    // Contract for the service that aggregates all calendar event sources.
    // =========================================================================

    /// <summary>
    /// Aggregates events from multiple domain services (tasks, holidays,
    /// birthdays, leave) and returns them as a unified list of
    /// <see cref="CalendarEvent"/> objects ready for the calendar grid.
    /// </summary>
    public interface ICalendarService
    {
        /// <summary>
        /// Returns all calendar events whose date range overlaps the window
        /// defined by <paramref name="start"/> and <paramref name="end"/>.
        /// </summary>
        /// <param name="start">First day of the visible calendar window (inclusive).</param>
        /// <param name="end">Last day of the visible calendar window (inclusive).</param>
        /// <param name="projectIds">
        /// Optional filter: when provided, only tasks belonging to one of these
        /// project IDs are included.  Pass <c>null</c> to include all projects.
        /// </param>
        Task<List<CalendarEvent>> GetEventsAsync(
            DateTime start,
            DateTime end,
            IEnumerable<Guid>? projectIds = null);
    }
}
