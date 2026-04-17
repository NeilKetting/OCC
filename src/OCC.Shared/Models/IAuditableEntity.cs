using System;

namespace OCC.Shared.Models
{
    public interface IAuditableEntity
    {
        DateTime CreatedAtUtc { get; set; }
        string CreatedBy { get; set; }
        DateTime? UpdatedAtUtc { get; set; }
        string? UpdatedBy { get; set; }
    }
}
