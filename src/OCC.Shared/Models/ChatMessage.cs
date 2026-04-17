using System;
using System.Collections.Generic;

namespace OCC.Shared.Models
{
    public class ChatMessage : BaseEntity
    {
        public Guid ChatSessionId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool HasAttachment { get; set; }
        public DateTime SentDate { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ChatSession? ChatSession { get; set; }
        public virtual User? Sender { get; set; }
        public virtual ICollection<ChatMessageAttachment> Attachments { get; set; } = new List<ChatMessageAttachment>();
        public virtual ICollection<ChatMessageReadReceipt> ReadReceipts { get; set; } = new List<ChatMessageReadReceipt>();
    }
}
