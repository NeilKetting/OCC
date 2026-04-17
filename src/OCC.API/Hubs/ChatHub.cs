using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System.Security.Claims;

namespace OCC.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Join a group for their own user ID so we can message them directly
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(Guid chatSessionId, string content)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var senderId)) return;

            var session = await _context.ChatSessions
                .Include(cs => cs.SessionUsers)
                .FirstOrDefaultAsync(cs => cs.Id == chatSessionId);

            if (session == null || !session.SessionUsers.Any(su => su.UserId == senderId))
                return; // Unauthorized or doesn't exist

            var sender = await _context.Users.FindAsync(senderId);
            var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Unknown";

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = chatSessionId,
                SenderId = senderId,
                Content = content,
                SentDate = DateTime.UtcNow,
                HasAttachment = false
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            // Map to DTO to avoid serialization cycles (entity -> session -> users -> session...)
            var dto = new ChatMessageDto
            {
                Id = message.Id,
                ChatSessionId = message.ChatSessionId,
                SenderId = message.SenderId,
                SenderName = senderName,
                Content = message.Content,
                HasAttachment = message.HasAttachment,
                SentDate = message.SentDate
            };

            // Broadcast to all users in the session
            foreach (var user in session.SessionUsers)
            {
                await Clients.Group($"User_{user.UserId}").SendAsync("ReceiveMessage", dto);
            }
        }

        public async Task MarkAsRead(Guid messageId)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return;

            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message != null)
            {
                var existingReceipt = await _context.ChatMessageReadReceipts
                    .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId);

                if (existingReceipt == null)
                {
                    var receipt = new ChatMessageReadReceipt
                    {
                        Id = Guid.NewGuid(),
                        MessageId = messageId,
                        UserId = userId,
                        ReadDate = DateTime.UtcNow
                    };
                    _context.ChatMessageReadReceipts.Add(receipt);
                    await _context.SaveChangesAsync();
                    
                    // Notify sender that the message was read
                    await Clients.Group($"User_{message.SenderId}").SendAsync("MessageRead", messageId, userId);
                }
            }
        }

        public async Task MarkSessionAsRead(Guid chatSessionId)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return;

            // Find all messages in this session NOT sent by the current user
            // and where the current user doesn't have a read receipt yet
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ChatSessionId == chatSessionId && m.SenderId != userId)
                .Where(m => !_context.ChatMessageReadReceipts.Any(r => r.MessageId == m.Id && r.UserId == userId))
                .ToListAsync();

            if (unreadMessages.Any())
            {
                var receipts = unreadMessages.Select(m => new ChatMessageReadReceipt
                {
                    Id = Guid.NewGuid(),
                    MessageId = m.Id,
                    UserId = userId,
                    ReadDate = DateTime.UtcNow
                }).ToList();

                _context.ChatMessageReadReceipts.AddRange(receipts);
                await _context.SaveChangesAsync();

                // Notify senders that their messages were read by this user
                var groupedBySender = unreadMessages.GroupBy(m => m.SenderId);
                foreach (var group in groupedBySender)
                {
                    var messageIds = group.Select(m => m.Id).ToList();
                    await Clients.Group($"User_{group.Key}").SendAsync("MessagesRead", messageIds, userId);
                }
            }
        }

        public async Task<bool> ToggleFavourite(Guid chatSessionId)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return false;

            var sessionUser = await _context.ChatSessionUsers
                .FirstOrDefaultAsync(su => su.ChatSessionId == chatSessionId && su.UserId == userId);

            if (sessionUser != null)
            {
                sessionUser.IsFavourite = !sessionUser.IsFavourite;
                await _context.SaveChangesAsync();
                return sessionUser.IsFavourite;
            }

            return false;
        }
    }
}
