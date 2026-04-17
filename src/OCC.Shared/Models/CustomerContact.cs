using System;

namespace OCC.Shared.Models
{
    public class CustomerContact : BaseEntity
    {


        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty; 

        public Guid CustomerId { get; set; }
        // public virtual Customer Customer { get; set; } // Avoid circular cycle in JSON if easy, or use JsonIgnore. 
        // For simplicity in EF, we can keep the nav prop.
        [System.Text.Json.Serialization.JsonIgnore] 
        public virtual Customer? Customer { get; set; }
    }
}
