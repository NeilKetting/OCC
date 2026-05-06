using System;
using System.Collections.Generic;

namespace OCC.Shared.Models
{
    public class ChatSession : BaseEntity
    {
        public string? Name { get; set; }
        public bool IsGroupChat { get; set; }
        public string? SharedAesKey { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public Guid CreatedById { get; set; }
        
        // Navigation properties
        public virtual ICollection<ChatSessionUser> SessionUsers { get; set; } = new List<ChatSessionUser>();
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
