using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    public abstract class BaseEntity : IEntity, IAuditableEntity
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "System";

        public DateTime? UpdatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }

        public bool IsActive { get; set; } = true;
        
        public byte[]? RowVersion { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime CreatedAt => CreatedAtUtc.ToLocalTime();

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime? UpdatedAt => UpdatedAtUtc?.ToLocalTime();
    }
}
