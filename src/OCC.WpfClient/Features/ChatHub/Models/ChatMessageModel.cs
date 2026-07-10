using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.DTOs;
using System;

namespace OCC.WpfClient.Features.ChatHub.Models
{
    public partial class ChatMessageModel : ObservableObject
    {
        public ChatMessageDto Dto { get; }

        public Guid Id => Dto.Id;
        public string Content => Dto.Content;
        public DateTime SentDate => Dto.SentDate.ToLocalTime();
        public string SenderName => Dto.SenderName;
        public bool HasAttachment => Dto.HasAttachment;
        
        public string FirstAttachmentFileName
        {
            get
            {
                if (Dto.Attachments != null && Dto.Attachments.Count > 0)
                {
                    return Dto.Attachments[0].FileName;
                }
                return "Attachment";
            }
        }

        // UI Specific Properties
        [ObservableProperty]
        private bool _isMine;

        [ObservableProperty]
        private bool _showAvatar;

        [ObservableProperty]
        private string _displayTime;

        [ObservableProperty]
        private bool _isReply;

        [ObservableProperty]
        private string _replySender = string.Empty;

        [ObservableProperty]
        private string _replySnippet = string.Empty;

        [ObservableProperty]
        private string _displayContent = string.Empty;

        [ObservableProperty]
        private bool _isForwarded;

        public ChatMessageModel(ChatMessageDto dto, Guid currentUserId)
        {
            Dto = dto;
            _isMine = dto.SenderId == currentUserId;
            _showAvatar = !_isMine;
            _displayTime = dto.SentDate.ToLocalTime().ToString("t");

            var content = dto.Content;
            
            // Check if it is a reply
            if (content != null && content.StartsWith("[Reply to ") && content.Contains("]:\n"))
            {
                int endIndex = content.IndexOf("]:\n");
                if (endIndex > 10)
                {
                    string header = content.Substring(10, endIndex - 10);
                    int colonIndex = header.IndexOf(":");
                    if (colonIndex > 0)
                    {
                        IsReply = true;
                        ReplySender = header.Substring(0, colonIndex).Trim();
                        ReplySnippet = header.Substring(colonIndex + 1).Trim();
                        content = content.Substring(endIndex + 3);
                    }
                }
            }

            // Check if it is forwarded
            if (content != null && content.StartsWith("[Forwarded]:\n"))
            {
                IsForwarded = true;
                content = content.Substring(13);
            }

            DisplayContent = content ?? string.Empty;
        }
    }
}
