using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a construction project or site managed by Orange Circle Construction.
    /// This is a primary entity for tracking work, inventory allocation, and project management.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Projects</c> table. It serves as a container for <see cref="ProjectTask"/> and related resource allocations.
    /// <b>How:</b> Projects are associated with a <see cref="Customer"/> and can be assigned a <see cref="SiteManager"/> (linked to an <see cref="Employee"/>).
    /// Location data (Latitude/Longitude) allows for future mapping and site visit tracking.
    /// </remarks>
    public class Project : BaseEntity
    {


        /// <summary>
        /// The display name of the project (e.g., "Engen Bendor").
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A detailed description of the project scope and objectives.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The scheduled start date for the construction work.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// The estimated or actual completion date.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// House number and street name.
        /// </summary>
        public string StreetLine1 { get; set; } = string.Empty;

        /// <summary>
        /// Apartment, unit, suite, complex, etc.
        /// </summary>
        public string? StreetLine2 { get; set; }

        /// <summary>
        /// City or town.
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// State, province, or region.
        /// </summary>
        public string StateOrProvince { get; set; } = string.Empty;

        /// <summary>
        /// Postal or ZIP code.
        /// </summary>
        public string PostalCode { get; set; } = string.Empty;

        /// <summary>
        /// The country (e.g., "South Africa").
        /// </summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>
        /// GPS Latitude coordinate for navigation and geo-fencing.
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// GPS Longitude coordinate for navigation and geo-fencing.
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Current lifecycle status (e.g., "Planning", "Active", "Completed", "OnHold").
        /// </summary>
        public string Status { get; set; } = "Planning";
        
        /// <summary>
        /// General location or branch branding for the project.
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Name of the overall project manager (often office-based).
        /// </summary>
        public string ProjectManager { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to the <see cref="Employee"/> assigned as the Site Manager.
        /// </summary>
        public Guid? SiteManagerId { get; set; }

        /// <summary>
        /// Navigation property to the Site Manager entity.
        /// </summary>
        public virtual Employee? SiteManager { get; set; }

        /// <summary>
        /// String representation of the customer name (legacy/quick reference).
        /// </summary>
        public string Customer { get; set; } = string.Empty;

        /// <summary>
        /// Project priority level (e.g., "High", "Medium", "Low").
        /// </summary>
        public string Priority { get; set; } = "Medium";

        /// <summary>
        /// An abbreviated name used for reports and small UI elements.
        /// </summary>
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// The time site operations typically commence (used for attendance validation).
        /// </summary>
        public TimeSpan WorkStartTime { get; set; } = new TimeSpan(8, 0, 0);

        /// <summary>
        /// The time site operations typically conclude.
        /// </summary>
        public TimeSpan WorkEndTime { get; set; } = new TimeSpan(17, 0, 0);

        /// <summary>
        /// Standard lunch break duration in minutes.
        /// </summary>
        public int LunchDurationMinutes { get; set; } = 60;

        /// <summary>
        /// Collection of tasks associated with this project.
        /// </summary>
        public virtual ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
        
        /// <summary>
        /// Foreign key to the <see cref="Customer"/> entity.
        /// </summary>
        public Guid? CustomerId { get; set; }

        /// <summary>
        /// Gets a human-readable formatted address string.
        /// </summary>
        public string FullAddress => string.Join(", ", new[] { StreetLine1, StreetLine2, City, StateOrProvince, PostalCode, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>
        /// Navigation property to the associated Customer entity.
        /// </summary>
        public virtual Customer? CustomerEntity { get; set; }

        /// <summary>
        /// Collection of variation orders for this project.
        /// </summary>
        public virtual ICollection<ProjectVariationOrder> VariationOrders { get; set; } = new List<ProjectVariationOrder>();
    }
}
