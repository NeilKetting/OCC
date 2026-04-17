using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a client or customer of Orange Circle Construction.
    /// Customers are associated with one or more <see cref="Project"/>.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Customers</c> table.
    /// <b>How:</b> Often used as a high-level grouping for projects and billing.
    /// </remarks>
    public class Customer : BaseEntity
    {


        /// <summary> The full name of the customer company or individual. </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary> Optional grouping or header name for UI display. </summary>
        public string Header { get; set; } = string.Empty;

        /// <summary> Primary business email for the customer account. </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary> Primary contact phone number. </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary> Registered or billing address. </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary> Collection of contact persons for this customer. </summary>
        public virtual System.Collections.Generic.ICollection<CustomerContact> Contacts { get; set; } = new System.Collections.Generic.List<CustomerContact>();
    }
}

