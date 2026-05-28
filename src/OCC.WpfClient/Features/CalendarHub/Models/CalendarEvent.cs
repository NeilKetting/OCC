using System;

namespace OCC.WpfClient.Features.CalendarHub.Models
{
    // =========================================================================
    // CalendarEvent.cs
    // Shared domain models for the unified Calendar feature.
    // All calendar data sources (Tasks, Holidays, Birthdays, Leave) are
    // normalised into CalendarEvent objects before being displayed.
    // =========================================================================

    #region Enumerations

    /// <summary>
    /// Identifies the source/type of a calendar event so the UI can apply
    /// appropriate colours, icons, and filter toggles.
    /// </summary>
    public enum CalendarEventType
    {
        /// <summary>A construction project task (StartDate – FinishDate span).</summary>
        Task,

        /// <summary>A South African public holiday.</summary>
        PublicHoliday,

        /// <summary>An active employee's birthday (recurs annually on month/day).</summary>
        Birthday,

        /// <summary>An approved employee leave / absence block.</summary>
        Leave,

        /// <summary>A project milestone (future use).</summary>
        ProjectMilestone,

        /// <summary>An order delivery date from procurement (future use).</summary>
        OrderDelivery,

        /// <summary>A meeting (future use).</summary>
        Meeting,

        /// <summary>A to-do item or reminder (future use).</summary>
        ToDo
    }

    /// <summary>
    /// Describes where in a multi-day span this instance of the event sits,
    /// so the XAML renderer can round only the correct corners of the event bar.
    /// </summary>
    public enum CalendarEventSpan
    {
        /// <summary>Event starts and ends on the same calendar day.</summary>
        Single,

        /// <summary>First day of a multi-day event.</summary>
        Start,

        /// <summary>A day in the middle of a multi-day event.</summary>
        Middle,

        /// <summary>Last day of a multi-day event.</summary>
        End
    }

    #endregion

    #region CalendarEvent

    /// <summary>
    /// A normalised calendar event that can represent any source
    /// (Task, Holiday, Birthday, Leave, etc.).  The <see cref="OriginalSource"/>
    /// property holds the raw domain object so callers can navigate back to it.
    /// </summary>
    public class CalendarEvent
    {
        /// <summary>Unique identifier – either from the source record or a new <see cref="Guid"/>.</summary>
        public Guid Id { get; set; }

        /// <summary>Categorises the event for filtering and colour selection.</summary>
        public CalendarEventType Type { get; set; }

        /// <summary>Short display label shown in the day-cell event bar.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional longer text shown in the tooltip / overflow popup.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Inclusive start date of the event.</summary>
        public DateTime StartDate { get; set; }

        /// <summary>Inclusive end date of the event (same as StartDate for single-day events).</summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Hex colour string (e.g. "#3B82F6") used to paint the event bar background.
        /// Defaults to the accent blue.
        /// </summary>
        public string Color { get; set; } = "#2E9DFF";

        /// <summary>True if the underlying task / action has been completed.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Set by <c>CalendarHubViewModel.GenerateCalendar()</c> to indicate where in
        /// a multi-day span this cell instance sits, so corner rounding is correct.
        /// </summary>
        public CalendarEventSpan Span { get; set; } = CalendarEventSpan.Single;

        /// <summary>
        /// The name of the project this task belongs to.
        /// Populated for <see cref="CalendarEventType.Task"/> events; empty otherwise.
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Reference to the original domain object (ProjectTask, PublicHoliday,
        /// Employee, LeaveRequest, etc.) for navigation / drill-down.
        /// </summary>
        public object? OriginalSource { get; set; }
    }

    #endregion
}
