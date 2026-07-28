using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class UsersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<UsersController>> _mockLogger;
        private readonly PasswordHasher _passwordHasher;

        public UsersControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<UsersController>>();
            _passwordHasher = new PasswordHasher();
        }

        private (AppDbContext, UsersController) SetupController(string? userId = null, string role = "Admin", string? email = null)
        {
            var context = new AppDbContext(_dbOptions);
            var controller = new UsersController(context, _passwordHasher, _mockLogger.Object, null!);

            if (userId != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, role)
                };
                if (email != null)
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }
                var identity = new ClaimsIdentity(claims, "TestAuth");
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                };
            }

            return (context, controller);
        }

        [Fact]
        public async Task ChangePassword_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var currentUserId = Guid.NewGuid().ToString();
            var (_, controller) = SetupController(currentUserId);

            // Act
            var result = await controller.ChangePassword(null!);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ChangePassword_WrongOldPassword_ReturnsBadRequest()
        {
            // Arrange
            var userGuid = Guid.NewGuid();
            var currentUserId = userGuid.ToString();
            var (context, controller) = SetupController(currentUserId);

            var oldPassHash = _passwordHasher.HashPassword("OldSecurePass123!");
            context.Users.Add(new User
            {
                Id = userGuid,
                Email = "user@example.com",
                Password = oldPassHash,
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var req = new ChangePasswordRequest
            {
                OldPassword = "WrongOldPassword123!",
                NewPassword = "NewSecurePassword123!"
            };

            // Act
            var result = await controller.ChangePassword(req);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Incorrect current password.", badRequest.Value);
        }

        [Fact]
        public async Task ChangePassword_WeakNewPassword_ReturnsBadRequest()
        {
            // Arrange
            var userGuid = Guid.NewGuid();
            var currentUserId = userGuid.ToString();
            var (context, controller) = SetupController(currentUserId);

            var oldPassHash = _passwordHasher.HashPassword("OldSecurePass123!");
            context.Users.Add(new User
            {
                Id = userGuid,
                Email = "user@example.com",
                Password = oldPassHash,
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var req = new ChangePasswordRequest
            {
                OldPassword = "OldSecurePass123!",
                NewPassword = "weak"
            };

            // Act
            var result = await controller.ChangePassword(req);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ChangePassword_ValidPasswords_UpdatesUserPassword()
        {
            // Arrange
            var userGuid = Guid.NewGuid();
            var currentUserId = userGuid.ToString();
            var (context, controller) = SetupController(currentUserId);

            var oldPass = "OldSecurePass123!";
            var newPass = "NewSecurePass123!";
            context.Users.Add(new User
            {
                Id = userGuid,
                Email = "user@example.com",
                Password = _passwordHasher.HashPassword(oldPass),
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var req = new ChangePasswordRequest
            {
                OldPassword = oldPass,
                NewPassword = newPass
            };

            // Act
            var result = await controller.ChangePassword(req);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Password updated successfully.", okResult.Value);

            var dbUser = await context.Users.FirstAsync(u => u.Id == userGuid);
            Assert.True(_passwordHasher.VerifyPassword(newPass, dbUser.Password));
        }

        [Fact]
        public async Task GetUser_OwnProfile_ReturnsUser()
        {
            // Arrange
            var userGuid = Guid.NewGuid();
            var (context, controller) = SetupController(userGuid.ToString(), role: "Guest");

            var user = new User { Id = userGuid, Email = "self@example.com", FirstName = "Self" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetUser(userGuid);

            // Assert
            var actionResult = Assert.IsType<ActionResult<User>>(result);
            Assert.Equal(userGuid, actionResult.Value?.Id);
        }

        [Fact]
        public async Task GetUser_OtherUserProfileNonAdmin_ReturnsForbid()
        {
            // Arrange
            var currentUserId = Guid.NewGuid().ToString();
            var targetUserId = Guid.NewGuid();
            var (_, controller) = SetupController(currentUserId, role: "Guest");

            // Act
            var result = await controller.GetUser(targetUserId);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }
    }
}
