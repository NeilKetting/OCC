using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OCC.Shared.Models
{
    public class TaskAttachment : BaseEntity
    {


        public Guid TaskId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string FileSize { get; set; } = string.Empty;

        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("TaskId")]
        public virtual ProjectTask? Task { get; set; }
    }
}
