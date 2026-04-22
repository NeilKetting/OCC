using System;

namespace OCC.Shared.DTOs
{
    public class SubContractorSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Specialties { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string PerformanceTier { get; set; } = "Silver";
        public string ColorTheme { get; set; } = string.Empty;
    }
}
