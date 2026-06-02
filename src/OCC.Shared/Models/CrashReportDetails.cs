using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents serialized details of a application crash, cached locally on the client 
    /// before being uploaded to the server on subsequent startup.
    /// </summary>
    public class CrashReportDetails
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ExceptionMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // e.g. "AppDomain.UnhandledException"
        public string AppVersion { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ActiveView { get; set; }
    }
}
