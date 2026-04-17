using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.Models;
using System.Security.Claims;
using Xunit;

namespace OCC.Tests.API.Hubs
{
    public class ChatHubTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public ChatHubTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private (AppDbContext, ChatHub, Mock<IHubCallerClients>, Mock<IGroupManager>, Mock<HubCallerContext>) GetHub(Guid userId)
        {
            var context = new AppDbContext(_dbOptions);
            var mockClients = new Mock<IHubCallerClients>();
            var mockGroups = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);

            mockContext.Setup(c => c.User).Returns(principal);
            mockContext.Setup(c => c.ConnectionId).Returns(Guid.NewGuid().ToString());

            var hub = new ChatHub(context)
            {
                Clients = mockClients.Object,
                Groups = mockGroups.Object,
                Context = mockContext.Object
            };

            return (context, hub, mockClients, mockGroups, mockContext);
        }

        [Fact]
        public async Task SendMessage_SavesToDbAndBroadcastsToAllParticipants()
        {
            // Arrange
            var senderId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var (context, hub, mockClients, _, _) = GetHub(senderId);

            var session = new ChatSession { Id = sessionId, IsGroupChat = false };
            context.ChatSessions.Add(session);
            context.ChatSessionUsers.Add(new ChatSessionUser { ChatSessionId = sessionId, UserId = senderId });
            context.ChatSessionUsers.Add(new ChatSessionUser { ChatSessionId = sessionId, UserId = recipientId });
            await context.SaveChangesAsync();

            var mockClientProxy = new Mock<IClientProxy>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

            // Act
            await hub.SendMessage(sessionId, "Hello test");

            // Assert
            var savedMessage = await context.ChatMessages.FirstOrDefaultAsync();
            Assert.NotNull(savedMessage);
            Assert.Equal("Hello test", savedMessage.Content);
            Assert.Equal(senderId, savedMessage.SenderId);

            // Verify broadcast to both users
            mockClients.Verify(c => c.Group($"User_{senderId}"), Times.Once);
            mockClients.Verify(c => c.Group($"User_{recipientId}"), Times.Once);
            mockClientProxy.Verify(p => p.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default), Times.Exactly(2));
        }

        [Fact]
        public async Task MarkSessionAsRead_CreatesReadReceiptsForUnreadMessages()
        {
            // Arrange
            var readerId = Guid.NewGuid();
            var senderId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var (context, hub, mockClients, _, _) = GetHub(readerId);

            context.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ChatSessionId = sessionId, SenderId = senderId, Content = "Msg 1", SentDate = DateTime.UtcNow.AddMinutes(-2) });
            context.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ChatSessionId = sessionId, SenderId = senderId, Content = "Msg 2", SentDate = DateTime.UtcNow.AddMinutes(-1) });
            // Already read message
            var readMsgId = Guid.NewGuid();
            context.ChatMessages.Add(new ChatMessage { Id = readMsgId, ChatSessionId = sessionId, SenderId = senderId, Content = "Msg 3", SentDate = DateTime.UtcNow.AddMinutes(-3) });
            context.ChatMessageReadReceipts.Add(new ChatMessageReadReceipt { Id = Guid.NewGuid(), MessageId = readMsgId, UserId = readerId, ReadDate = DateTime.UtcNow.AddMinutes(-2) });
            
            await context.SaveChangesAsync();

            var mockClientProxy = new Mock<IClientProxy>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

            // Act
            await hub.MarkSessionAsRead(sessionId);

            // Assert
            var receipts = await context.ChatMessageReadReceipts.Where(r => r.UserId == readerId).ToListAsync();
            Assert.Equal(3, receipts.Count); // 1 original + 2 new

            // Verify sender notification
            mockClients.Verify(c => c.Group($"User_{senderId}"), Times.AtLeastOnce);
            mockClientProxy.Verify(p => p.SendCoreAsync("MessagesRead", It.IsAny<object[]>(), default), Times.Once);
        }
    }
}
