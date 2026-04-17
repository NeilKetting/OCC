using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents an immutable log of a physical clocking event (V2 System).
    /// Only records when a button was pressed or a card was swiped.
    /// </summary>
    public class ClockingEvent : BaseEntity
    {
        /// <summary> Foreign key to the <see cref="Employee"/> record. </summary>
        public Guid EmployeeId { get; set; }

        /// <summary> The exact timestamp the event occurred. </summary>
        public DateTime Timestamp { get; set; }

        /// <summary> Whether this is a clock-in or clock-out event. </summary>
        public ClockEventType EventType { get; set; }

        /// <summary> The origin of the event (e.g., "WebPortal", "MobileApp", "BiometricScanner"). </summary>
        public string Source { get; set; } = "Unknown";
    }

    public enum ClockEventType
    {
        ClockIn,
        ClockOut
    }
}
