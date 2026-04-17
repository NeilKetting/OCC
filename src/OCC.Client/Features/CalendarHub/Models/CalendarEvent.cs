using System;

namespace OCC.Client.Features.CalendarHub.Models
{
    public enum CalendarEventType
    {
        Task,
        Meeting,
        ToDo,
        Birthday,
        PublicHoliday,
        ProjectMilestone,
        Leave,
        OrderDelivery
    }

    public enum CalendarEventSpan
    {
        Single,
        Start,
        Middle,
        End
    }

    public class CalendarEvent
    {
        public Guid Id { get; set; }
        public CalendarEventType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Color { get; set; } = "#3B82F6";
        public bool IsCompleted { get; set; }
        public CalendarEventSpan Span { get; set; } = CalendarEventSpan.Single;
        public object? OriginalSource { get; set; } // Reference to original Task, Employee, etc.
    }
}
