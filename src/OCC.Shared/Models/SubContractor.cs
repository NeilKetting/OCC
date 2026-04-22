using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a sub-contractor company that provides specialized services to projects.
    /// </summary>
    public class SubContractor : BaseEntity
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        /// <summary>
        /// Comma-separated list of construction specialties or JSON string.
        /// e.g. "Electrical, Plumbing, HVAC"
        /// </summary>
        public string? Specialties { get; set; }

        public string Branch { get; set; } = "Johannesburg";

        /// <summary>
        /// A hex color assigned to uniquely identify this SubContractor visually in tracking dashboards.
        /// </summary>
        public string ColorTheme { get; set; } = string.Empty;
    }
}
