using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    public class AuthControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<AuthService>> _mockAuthServiceLogger;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher _passwordHasher;

        public AuthControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockAuthServiceLogger = new Mock<ILogger<AuthService>>();
            _mockEmailService = new Mock<IEmailService>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            _passwordHasher = new PasswordHasher();

            var inMemorySettings = new Dictionary<string, string?> {
                {"Jwt:Key", "SuperSecretKeyForTestingMustBeAtLeast32BytesLength!"},
                {"Jwt:Issuer", "OCC.API"},
                {"Jwt:Audience", "OCC.Client"}
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        private (AppDbContext, AuthController) SetupController()
        {
            var context = new AppDbContext(_dbOptions);
            var authService = new AuthService(
                context,
                _configuration,
                _passwordHasher,
                null!, // SignalR hub context null safe
                _mockEmailService.Object,
                _mockHttpContextAccessor.Object,
                _mockAuthServiceLogger.Object
            );
            var controller = new AuthController(authService, null!);
            return (context, controller);
        }

        [Fact]
        public void Ping_ReturnsOkResult_WithTimestamp()
        {
            // Arrange
            var (_, controller) = SetupController();

            // Act
            var result = controller.Ping();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Login_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var (_, controller) = SetupController();
            var request = new LoginRequest { Email = "", Password = "" };

            // Act
            var result = await controller.Login(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "test@example.com";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword("SecurePass123!"),
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var request = new LoginRequest { Email = email, Password = "WrongPassword123!" };

            // Act
            var result = await controller.Login(request);

            // Assert
            var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Invalid credentials.", unauthResult.Value);
        }

        [Fact]
        public async Task Login_PendingApproval_ReturnsForbidden()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "pending@example.com";
            var password = "SecurePassword123!";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword(password),
                IsApproved = false
            });
            await context.SaveChangesAsync();

            var request = new LoginRequest { Email = email, Password = password };

            // Act
            var result = await controller.Login(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, objectResult.StatusCode);
        }

        [Fact]
        public async Task Login_Successful_ReturnsOkWithTokenAndUser()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "approved@example.com";
            var password = "SecurePassword123!";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword(password),
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var request = new LoginRequest { Email = email, Password = password };

            // Act
            var result = await controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Logout_UserAuthenticated_ReturnsOk()
        {
            // Arrange
            var (_, controller) = SetupController();
            var userId = Guid.NewGuid().ToString();
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            // Act
            var result = await controller.Logout();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Register_InvalidData_ReturnsBadRequest()
        {
            // Arrange
            var (_, controller) = SetupController();

            // Act
            var result = await controller.Register(null!);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Register_ExistingUser_ReturnsConflict()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "existing@example.com";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword("SecurePassword123!"),
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var newUser = new User
            {
                Email = email,
                Password = "SecurePassword123!",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = await controller.Register(newUser);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal("User already exists", conflictResult.Value);
        }

        [Fact]
        public async Task Register_WeakPassword_ReturnsBadRequest()
        {
            // Arrange
            var (_, controller) = SetupController();
            var newUser = new User
            {
                Email = "weakpass@example.com",
                Password = "123",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = await controller.Register(newUser);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Register_ValidUser_ReturnsOk()
        {
            // Arrange
            var (_, controller) = SetupController();
            var newUser = new User
            {
                Email = "newuser@example.com",
                Password = "SecurePassword123!",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = await controller.Register(newUser);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedUser = Assert.IsType<User>(okResult.Value);
            Assert.Equal(newUser.Email, returnedUser.Email);
            Assert.False(returnedUser.IsApproved);
        }

        [Fact]
        public async Task VerifyEmail_NullOrEmptyToken_ReturnsBadRequest()
        {
            // Arrange
            var (_, controller) = SetupController();

            // Act
            var result = await controller.VerifyEmail("");

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task VerifyEmail_InvalidToken_ReturnsBadRequest()
        {
            // Arrange
            var (_, controller) = SetupController();

            // Act
            var result = await controller.VerifyEmail("invalid.jwt.token");

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ForgotPassword_ValidEmail_GeneratesCodeAndSendsEmail()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "neil@mdk.co.za";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FirstName = "Neil",
                LastName = "Ketting",
                Password = "hashedpassword",
                IsApproved = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new ForgotPasswordRequest { Email = email };

            // Act
            var result = await controller.ForgotPassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            // Verify code saved in DB
            var dbUser = await context.Users.FirstAsync(u => u.Email == email);
            Assert.NotNull(dbUser.PasswordResetCode);
            Assert.True(dbUser.PasswordResetCodeExpiry > DateTime.UtcNow);

            // Verify email sent
            _mockEmailService.Verify(e => e.SendEmailAsync(
                email,
                It.Is<string>(s => s.Contains("Reset")),
                It.Is<string>(b => b.Contains(dbUser.PasswordResetCode))
            ), Times.Once);
        }

        [Fact]
        public async Task ForgotPassword_InvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            var (_, controller) = SetupController();
            var request = new ForgotPasswordRequest { Email = "nonexistent@test.com" };

            // Act
            var result = await controller.ForgotPassword(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ResetPassword_ValidCode_UpdatesPassword()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "neil@mdk.co.za";
            var oldPasswordHash = _passwordHasher.HashPassword("OldPassword123!");
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FirstName = "Neil",
                LastName = "Ketting",
                Password = oldPasswordHash,
                PasswordResetCode = "123456",
                PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(10),
                IsApproved = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Email = email,
                Code = "123456",
                NewPassword = "NewPassword123!"
            };

            // Act
            var result = await controller.ResetPassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            // Verify code cleared and password updated
            var dbUser = await context.Users.FirstAsync(u => u.Email == email);
            Assert.Null(dbUser.PasswordResetCode);
            Assert.Null(dbUser.PasswordResetCodeExpiry);
            Assert.True(_passwordHasher.VerifyPassword("NewPassword123!", dbUser.Password));
        }

        [Fact]
        public async Task ResetPassword_ExpiredCode_ReturnsBadRequest()
        {
            // Arrange
            var (context, controller) = SetupController();
            var email = "neil@mdk.co.za";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = "hashedpassword",
                PasswordResetCode = "123456",
                PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(-5), // Expired
                IsApproved = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new ResetPasswordRequest
            {
                Email = email,
                Code = "123456",
                NewPassword = "NewPassword123!"
            };

            // Act
            var result = await controller.ResetPassword(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
