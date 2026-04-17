using System;

namespace OCC.Shared.Models
{
    public class ChatSessionUser : BaseEntity
    {
        public Guid ChatSessionId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
        public bool IsFavourite { get; set; }
        
        /// <summary> The AES Symmetric Key for this Session, encrypted using this User's RSA Public Key. </summary>
        public string? EncryptedAesKey { get; set; }

        // Navigation
        public virtual ChatSession? ChatSession { get; set; }
        public virtual User? User { get; set; }
    }
}
