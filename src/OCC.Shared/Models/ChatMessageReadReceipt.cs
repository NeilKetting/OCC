using System;

namespace OCC.Shared.Models
{
    public class ChatMessageReadReceipt : BaseEntity
    {
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public DateTime? ReadDate { get; set; }

        public virtual ChatMessage? Message { get; set; }
        public virtual User? User { get; set; }
    }
}
