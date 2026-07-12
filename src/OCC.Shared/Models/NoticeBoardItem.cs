using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Shared.Models
{
    public enum NoticeCategory
    {
        Announcement,
        BugTesting,
        Maintenance
    }

    public class NoticeBoardItem : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public NoticeCategory Category { get; set; } = NoticeCategory.Announcement;

        public DateTime? ExpiryDate { get; set; }

        public bool IsPinned { get; set; }
    }
}
