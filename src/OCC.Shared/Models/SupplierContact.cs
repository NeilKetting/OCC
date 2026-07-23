using System;

namespace OCC.Shared.Models
{
    public class SupplierContact : BaseEntity
    {
        public string ContactName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;

        public Guid SupplierId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public virtual Supplier? Supplier { get; set; }
    }
}
