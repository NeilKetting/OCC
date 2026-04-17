using System;

namespace OCC.Shared.DTOs
{
    public class CustomerSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
    }

    public class HseqTrainingSummaryDto
    {
        public Guid Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string TrainingTopic { get; set; } = string.Empty;
        public string CertificateType { get; set; } = string.Empty;
        public DateTime DateCompleted { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Role { get; set; } = string.Empty;
        public string CertificateUrl { get; set; } = string.Empty;
        public string Trainer { get; set; } = string.Empty;
    }
}
