using System;

namespace OCC.Shared.Models
{
    public class ChatMessageAttachment : BaseEntity
    {
        public Guid MessageId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }

        public virtual ChatMessage? Message { get; set; }
    }
}
