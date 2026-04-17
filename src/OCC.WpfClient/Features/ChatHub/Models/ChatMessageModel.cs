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

        // UI Specific Properties
        [ObservableProperty]
        private bool _isMine;

        [ObservableProperty]
        private bool _showAvatar;

        [ObservableProperty]
        private string _displayTime;

        public ChatMessageModel(ChatMessageDto dto, Guid currentUserId)
        {
            Dto = dto;
            _isMine = dto.SenderId == currentUserId;
            _showAvatar = !_isMine; // Usually we only show avatars for the other person
            _displayTime = dto.SentDate.ToLocalTime().ToString("t"); // e.g. "14:30"
        }
    }
}
