using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Security.Claims;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class MessagesControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<IHubContext<ChatHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public MessagesControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockHubContext = new Mock<IHubContext<ChatHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        }

        private (AppDbContext, MessagesController) GetController(Guid currentUserId)
        {
            var context = new AppDbContext(_dbOptions);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var controllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var controller = new MessagesController(context, _mockHubContext.Object)
            {
                ControllerContext = controllerContext
            };

            return (context, controller);
        }

        [Fact]
        public async Task GetMySessions_ReturnsOnlyUserSessions()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var (context, controller) = GetController(userId);

            var session1 = new ChatSession { Id = Guid.NewGuid(), Name = "Session 1" };
            var session2 = new ChatSession { Id = Guid.NewGuid(), Name = "Session 2" };

            context.ChatSessions.AddRange(session1, session2);
            context.ChatSessionUsers.Add(new ChatSessionUser { ChatSessionId = session1.Id, UserId = userId });
            context.ChatSessionUsers.Add(new ChatSessionUser { ChatSessionId = session2.Id, UserId = otherUserId });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetMySessions();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var sessions = Assert.IsType<List<ChatSessionDto>>(okResult.Value);
            Assert.Single(sessions);
            Assert.Equal(session1.Id, sessions[0].Id);
        }

        [Fact]
        public async Task CreateGroupSession_SavesToDbAndNotifiesParticipants()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var participantId = Guid.NewGuid();
            var (context, controller) = GetController(userId);

            // Add users to DB first because of FK constraints in real world (though InMemory is relaxed)
            context.Users.Add(new User { Id = userId, FirstName = "User", LastName = "One", Email = "one@test.com" });
            context.Users.Add(new User { Id = participantId, FirstName = "User", LastName = "Two", Email = "two@test.com" });
            await context.SaveChangesAsync();

            var request = new MessagesController.CreateGroupSessionRequest
            {
                Name = "Test Group",
                Participants = new List<MessagesController.GroupParticipantKey>
                {
                    new MessagesController.GroupParticipantKey { UserId = userId, EncryptedAesKey = "key1" },
                    new MessagesController.GroupParticipantKey { UserId = participantId, EncryptedAesKey = "key2" }
                }
            };

            // Act
            var result = await controller.CreateGroupSession(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            // Verify DB entries
            var session = await context.ChatSessions.Include(cs => cs.SessionUsers).FirstOrDefaultAsync();
            Assert.NotNull(session);
            Assert.Equal("Test Group", session.Name);
            Assert.Equal(2, session.SessionUsers.Count);

            // Verify SignalR notifications
            _mockClients.Verify(c => c.Group($"User_{userId}"), Times.Once);
            _mockClients.Verify(c => c.Group($"User_{participantId}"), Times.Once);
            _mockClientProxy.Verify(p => p.SendCoreAsync("SessionCreated", It.IsAny<object[]>(), default), Times.Exactly(2));
        }

        [Fact]
        public async Task DeleteSession_OnlyAllowedByCreatorForGroups()
        {
            // Arrange
            var creatorId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            
            var (context, controller) = GetController(otherUserId); // Logged in as non-creator

            var session = new ChatSession { Id = sessionId, IsGroupChat = true, CreatedById = creatorId };
            context.ChatSessions.Add(session);
            context.ChatSessionUsers.Add(new ChatSessionUser { ChatSessionId = sessionId, UserId = otherUserId });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DeleteSession(sessionId);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
    }
}
