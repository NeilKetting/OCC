using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System.Security.Claims;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public MessagesController(AppDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private Guid GetCurrentUserId()
        {
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetMySessions()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var sessions = await _context.ChatSessions
                .Include(cs => cs.SessionUsers)
                    .ThenInclude(su => su.User)
                .Include(cs => cs.Messages.OrderByDescending(m => m.SentDate).Take(1)) // Get latest message for preview
                .Where(cs => cs.SessionUsers.Any(su => su.UserId == userId))
                .OrderByDescending(cs => cs.Messages.Max(m => (DateTime?)m.SentDate) ?? cs.CreatedAtUtc)
                .Select(cs => new ChatSessionDto
                {
                    Id = cs.Id,
                    Name = cs.Name,
                    IsGroupChat = cs.IsGroupChat,
                    CreatedAtUtc = cs.CreatedAtUtc,
                    CreatedById = cs.CreatedById,
                    UnreadCount = cs.Messages.Count(m => m.SenderId != userId && !m.ReadReceipts.Any(r => r.UserId == userId)),
                    IsFavourite = cs.SessionUsers.Where(su => su.UserId == userId).Select(su => su.IsFavourite).FirstOrDefault(),
                    SharedAesKey = cs.SharedAesKey,
                    Users = cs.SessionUsers.Select(su => new ChatUserDto { 
                        UserId = su.UserId, 
                        FirstName = su.User!.FirstName, 
                        LastName = su.User.LastName, 
                        Email = su.User.Email,
                        PublicKey = su.User!.PublicKey,
                        EncryptedAesKey = su.EncryptedAesKey
                    }).ToList(),
                    LastMessage = cs.Messages.OrderByDescending(m => m.SentDate).Select(m => new ChatMessageDto
                    {
                        Id = m.Id,
                        ChatSessionId = m.ChatSessionId,
                        SenderId = m.SenderId,
                        SenderName = m.Sender != null ? $"{m.Sender.FirstName} {m.Sender.LastName}" : "Unknown",
                        Content = m.Content,
                        HasAttachment = m.HasAttachment,
                        SentDate = m.SentDate
                    }).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("sessions/{sessionId}/messages")]
        public async Task<IActionResult> GetMessages(Guid sessionId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var isMember = await _context.ChatSessionUsers.AnyAsync(su => su.ChatSessionId == sessionId && su.UserId == userId);
            if (!isMember) return Forbid();

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Attachments)
                .Include(m => m.ReadReceipts)
                .Where(m => m.ChatSessionId == sessionId)
                .OrderByDescending(m => m.SentDate)
                .Skip(skip)
                .Take(take)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    ChatSessionId = m.ChatSessionId,
                    SenderId = m.SenderId,
                    SenderName = m.Sender != null ? $"{m.Sender.FirstName} {m.Sender.LastName}" : "Unknown",
                    Content = m.Content,
                    HasAttachment = m.HasAttachment,
                    SentDate = m.SentDate,
                    Attachments = m.Attachments.Select(a => new ChatAttachmentDto { 
                        Id = a.Id, FileName = a.FileName, FilePath = a.FilePath, FileType = a.FileType, FileSize = a.FileSize 
                    }).ToList(),
                    ReadBy = m.ReadReceipts.Select(r => new ChatReadReceiptDto { 
                        UserId = r.UserId, ReadDate = r.ReadDate 
                    }).ToList()
                })
                .ToListAsync();

            // Return in chronological order
            messages.Reverse();

            return Ok(messages);
        }

        public class CreateDirectSessionRequest
        {
            public string MyEncryptedAesKey { get; set; } = string.Empty;
            public string TargetEncryptedAesKey { get; set; } = string.Empty;
        }

        public class CreateGroupSessionRequest
        {
            public string Name { get; set; } = string.Empty;
            public List<GroupParticipantKey> Participants { get; set; } = new();
        }

        public class GroupParticipantKey
        {
            public Guid UserId { get; set; }
            public string EncryptedAesKey { get; set; } = string.Empty;
        }

        [HttpPost("direct/{targetUserId}")]
        public async Task<IActionResult> GetOrCreateDirectSession(Guid targetUserId, [FromBody] CreateDirectSessionRequest? request = null)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty) return Unauthorized();

            if (currentUserId == targetUserId) return BadRequest("Cannot chat with yourself.");

            // Check if direct session already exists
            var existingSession = await _context.ChatSessions
                .Include(cs => cs.SessionUsers)
                .Where(cs => !cs.IsGroupChat && 
                             cs.SessionUsers.Any(su => su.UserId == currentUserId) && 
                             cs.SessionUsers.Any(su => su.UserId == targetUserId))
                .FirstOrDefaultAsync();

            if (existingSession != null)
            {
                return Ok(new { SessionId = existingSession.Id });
            }

            if (request == null || string.IsNullOrEmpty(request.MyEncryptedAesKey) || string.IsNullOrEmpty(request.TargetEncryptedAesKey))
            {
                // Indicate to client that session doesn't exist and keys are required
                return Ok(new { RequiresKeys = true });
            }

            // Create new direct session
            var newSession = new ChatSession
            {
                Id = Guid.NewGuid(),
                IsGroupChat = false,
                Name = null,
                CreatedById = currentUserId,
                SharedAesKey = GenerateAesKey()
            };
            
            _context.ChatSessions.Add(newSession);

            _context.ChatSessionUsers.Add(new ChatSessionUser { Id = Guid.NewGuid(), ChatSessionId = newSession.Id, UserId = currentUserId });
            _context.ChatSessionUsers.Add(new ChatSessionUser { Id = Guid.NewGuid(), ChatSessionId = newSession.Id, UserId = targetUserId });

            await _context.SaveChangesAsync();

            return Ok(new { SessionId = newSession.Id });
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadAttachment([FromForm] Guid chatSessionId, IFormFile? file)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            if (file == null || file.Length == 0) return BadRequest("No file provided.");

            var isMember = await _context.ChatSessionUsers.AnyAsync(su => su.ChatSessionId == chatSessionId && su.UserId == userId);
            if (!isMember) return Forbid();

            // Save File
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat", chatSessionId.ToString());
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var safeFileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create Message
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = chatSessionId,
                SenderId = userId,
                Content = "", // Or optional caption
                HasAttachment = true,
                SentDate = DateTime.UtcNow
            };
            
            _context.ChatMessages.Add(message);

            var attachment = new ChatMessageAttachment
            {
                Id = Guid.NewGuid(),
                MessageId = message.Id,
                FileName = safeFileName,
                FilePath = $"/uploads/chat/{chatSessionId}/{uniqueFileName}",
                FileType = file.ContentType,
                FileSize = file.Length
            };

            _context.ChatMessageAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            // Broadcast
            var sender = await _context.Users.FindAsync(userId);
            var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Unknown";
            
            var session = await _context.ChatSessions.Include(cs => cs.SessionUsers).FirstOrDefaultAsync(cs => cs.Id == chatSessionId);
            if (session != null)
            {
                foreach (var user in session.SessionUsers)
                {
                    // Include attachment details in the broadcast payload
                    var dto = new ChatMessageDto
                    {
                        Id = message.Id,
                        ChatSessionId = message.ChatSessionId,
                        SenderId = message.SenderId,
                        SenderName = senderName,
                        Content = message.Content,
                        HasAttachment = message.HasAttachment,
                        SentDate = message.SentDate,
                        Attachments = new List<ChatAttachmentDto> 
                        { 
                            new ChatAttachmentDto 
                            { 
                                Id = attachment.Id, 
                                FileName = attachment.FileName, 
                                FilePath = attachment.FilePath, 
                                FileType = attachment.FileType, 
                                FileSize = attachment.FileSize 
                            } 
                        }
                    };

                    await _hubContext.Clients.Group($"User_{user.UserId}").SendAsync("ReceiveMessage", dto);
                }
            }

            return Ok(message);
        }

        [HttpPost("groups")]
        public async Task<IActionResult> CreateGroupSession([FromBody] CreateGroupSessionRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Group name is required.");
            if (request.Participants == null || !request.Participants.Any()) return BadRequest("Participants are required.");

            // Create new group session
            var newSession = new ChatSession
            {
                Id = Guid.NewGuid(),
                IsGroupChat = true,
                Name = request.Name,
                CreatedById = currentUserId,
                CreatedAtUtc = DateTime.UtcNow,
                SharedAesKey = GenerateAesKey()
            };

            _context.ChatSessions.Add(newSession);

            // Add all participants (including creator)
            foreach (var participant in request.Participants)
            {
                _context.ChatSessionUsers.Add(new ChatSessionUser
                {
                    Id = Guid.NewGuid(),
                    ChatSessionId = newSession.Id,
                    UserId = participant.UserId,
                    JoinedDate = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Notify participants via SignalR (optional, or rely on client to fetch)
            foreach (var p in request.Participants)
            {
                await _hubContext.Clients.Group($"User_{p.UserId}").SendAsync("SessionCreated", newSession.Id);
            }

            return Ok(new { SessionId = newSession.Id });
        }

        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty) return Unauthorized();

            var session = await _context.ChatSessions
                .Include(cs => cs.SessionUsers)
                .FirstOrDefaultAsync(cs => cs.Id == sessionId);

            if (session == null) return NotFound();

            // permission check:
            // If group chat, only creator can delete.
            // If direct chat, it's personal? Or should both be able to delete? 
            // The request says "Person who creates the group will be the group dmin and only he can delete the group."
            if (session.IsGroupChat && session.CreatedById != currentUserId)
            {
                return Forbid("Only the group creator can delete this group.");
            }
            
            // For direct chat, let's allow either person to delete the "session" (which hides it for both).
            // Usually in apps like WhatsApp, deleting a chat only deletes it for you locally. 
            // In our DB model, deleting the ChatSession deletes it for everyone.
            if (!session.IsGroupChat && !session.SessionUsers.Any(su => su.UserId == currentUserId))
            {
                return Forbid();
            }

            // Delete messages and sessions
            // Note: Cascade delete should handle messages and session users if configured in EF
            _context.ChatSessions.Remove(session);
            await _context.SaveChangesAsync();

            // Notify all relevant users
            foreach (var user in session.SessionUsers)
            {
                await _hubContext.Clients.Group($"User_{user.UserId}").SendAsync("SessionDeleted", sessionId);
            }

            return NoContent();
        }

        [HttpPost("sessions/{sessionId}/exit")]
        public async Task<IActionResult> ExitGroup(Guid sessionId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == Guid.Empty) return Unauthorized();

            var sessionUser = await _context.ChatSessionUsers
                .Include(su => su.ChatSession)
                .FirstOrDefaultAsync(su => su.ChatSessionId == sessionId && su.UserId == currentUserId);

            if (sessionUser == null) return NotFound();
            if (sessionUser.ChatSession != null && !sessionUser.ChatSession.IsGroupChat) 
                return BadRequest("Cannot exit a direct chat.");

            _context.ChatSessionUsers.Remove(sessionUser);
            await _context.SaveChangesAsync();

            // Notify others
            await _hubContext.Clients.Group($"Session_{sessionId}").SendAsync("UserLeft", sessionId, currentUserId);
            // And notify the user themselves to remove it from their list
            await _hubContext.Clients.Group($"User_{currentUserId}").SendAsync("SessionDeleted", sessionId);

            return NoContent();
        }
        private string GenerateAesKey()
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            return Convert.ToBase64String(aes.Key);
        }
    }
}
