using System;
using System.Collections.Generic;

namespace OCC.Shared.DTOs
{
    public class ChatSessionDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public bool IsGroupChat { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int UnreadCount { get; set; }
        public bool IsFavourite { get; set; }
        public List<ChatUserDto> Users { get; set; } = new List<ChatUserDto>();
        public ChatMessageDto? LastMessage { get; set; }
    }

    public class ChatUserDto
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PublicKey { get; set; }
        public string? EncryptedAesKey { get; set; }
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid ChatSessionId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool HasAttachment { get; set; }
        public DateTime SentDate { get; set; }
        public List<ChatAttachmentDto> Attachments { get; set; } = new List<ChatAttachmentDto>();
        public List<ChatReadReceiptDto> ReadBy { get; set; } = new List<ChatReadReceiptDto>();
    }

    public class ChatAttachmentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class ChatReadReceiptDto
    {
        public Guid UserId { get; set; }
        public DateTime? ReadDate { get; set; }
    }
}
