using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OCC.WpfClient.Features.ChatHub.Models
{
    public partial class ChatSessionModel : ObservableObject
    {
        public ChatSessionDto Dto { get; }

        public Guid Id => Dto.Id;
        
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private bool _isGroupChat;

        [ObservableProperty]
        private string _lastMessagePreview;

        [ObservableProperty]
        private DateTime _lastMessageTime;

        [ObservableProperty]
        private int _unreadCount;

        [ObservableProperty]
        private bool _isCurrentUserAdmin;

        [ObservableProperty]
        private bool _isFavourite;

        [ObservableProperty]
        private string _initials = string.Empty;

        [ObservableProperty]
        private string _badgeColor = string.Empty;

        [ObservableProperty]
        private string? _profilePictureUrl;

        public Guid CreatedById => Dto.CreatedById;
        public List<ChatUserDto> Users => Dto.Users;

        public bool IsAdmin(Guid userId) => Dto.CreatedById == userId;

        public string DecryptedAesKey { get; set; } = string.Empty;

        public ObservableCollection<ChatMessageModel> Messages { get; } = new ObservableCollection<ChatMessageModel>();

        private readonly Guid _currentUserId;

        public ChatSessionModel(ChatSessionDto dto, Guid currentUserId)
        {
            Dto = dto;
            _currentUserId = currentUserId;
            IsGroupChat = dto.IsGroupChat;
            UnreadCount = dto.UnreadCount;
            IsFavourite = dto.IsFavourite;

            if (!IsGroupChat && dto.Users.Count >= 2)
            {
                var otherUser = dto.Users.FirstOrDefault(u => u.UserId != _currentUserId);
                Name = otherUser != null ? $"{otherUser.FirstName} {otherUser.LastName}" : (dto.Name ?? "Chat");
            }
            else
            {
                Name = dto.Name ?? "Chat";
            }
            
            if (dto.LastMessage != null)
            {
                LastMessagePreview = dto.LastMessage.HasAttachment ? "📎 Attachment" : dto.LastMessage.Content;
                LastMessageTime = dto.LastMessage.SentDate;
            }
            else
            {
                LastMessagePreview = "";
                LastMessageTime = dto.CreatedAtUtc;
            }

            // Set Initials and Color
            UpdateMetadata();
        }

        private void UpdateMetadata()
        {
            if (!IsGroupChat && Dto.Users.Count >= 2)
            {
                var otherUser = Dto.Users.FirstOrDefault(u => u.UserId != _currentUserId);
                if (otherUser != null)
                {
                    Initials = GetInitials(otherUser.FirstName, otherUser.LastName);
                    BadgeColor = GetColorFromGuid(otherUser.UserId);
                    // ProfilePictureUrl = otherUser.ProfilePictureUrl; // Not in Dto yet, but ready
                }
            }
            else
            {
                Initials = GetInitials(Name, "");
                BadgeColor = GetColorFromGuid(Id);
            }
        }

        private string GetInitials(string first, string last)
        {
            if (string.IsNullOrWhiteSpace(first)) return "?";
            if (string.IsNullOrWhiteSpace(last)) return first[0].ToString().ToUpper();
            return (first[0].ToString() + last[0].ToString()).ToUpper();
        }

        private string GetColorFromGuid(Guid id)
        {
            // Curated harmonious color palette (HSL tailored) - Matches UserColorConverter
            string[] colors = { 
                "#0ea5e9", // Sky 500
                "#10b981", // Emerald 500
                "#f59e0b", // Amber 500
                "#ef4444", // Red 500
                "#8b5cf6", // Violet 500
                "#ec4899", // Pink 500
                "#f97316", // Orange 500
                "#6366f1", // Indigo 500
                "#14b8a6", // Teal 500
                "#06b6d4", // Cyan 500
                "#84cc16", // Lime 500
                "#d946ef"  // Fuchsia 500
            };
            
            var bytes = id.ToByteArray();
            int hash = 17;
            foreach (var b in bytes)
            {
                hash = hash * 31 + b;
            }

            return colors[Math.Abs(hash) % colors.Length];
        }
    }
}
