using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a dynamic configuration value for the application.
    /// Allows changing system behavior without redeploying code.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>AppSettings</c> table.
    /// <b>How:</b> Loaded at startup or on-demand. Lookups are done via the unique <see cref="Key"/>.
    /// </remarks>
    public class AppSetting : BaseEntity
    {


        /// <summary> The unique identifier string for the setting (e.g., "TaxRate"). </summary>
        [Required]
        public string Key { get; set; } = string.Empty;

        /// <summary> The value of the setting (typically stored as a string, parsed by consumer). </summary>
        public string Value { get; set; } = string.Empty;
    }
}
