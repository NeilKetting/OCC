using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<AuthService>> _mockLogger;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher _passwordHasher;

        public AuthServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<AuthService>>();
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

        private (AppDbContext, AuthService) CreateAuthService()
        {
            var context = new AppDbContext(_dbOptions);
            var service = new AuthService(
                context,
                _configuration,
                _passwordHasher,
                null!, // HubContext optional
                _mockEmailService.Object,
                _mockHttpContextAccessor.Object,
                _mockLogger.Object
            );
            return (context, service);
        }

        [Fact]
        public async Task LoginAsync_NullOrEmptyRequest_ReturnsFailure()
        {
            // Arrange
            var (_, service) = CreateAuthService();

            // Act
            var resultNull = await service.LoginAsync(null!);
            var resultEmpty = await service.LoginAsync(new LoginRequest { Email = "", Password = "" });

            // Assert
            Assert.False(resultNull.Success);
            Assert.Equal("Invalid client request", resultNull.Error);
            Assert.False(resultEmpty.Success);
        }

        [Fact]
        public async Task LoginAsync_NonExistentUser_ReturnsFailure()
        {
            // Arrange
            var (_, service) = CreateAuthService();
            var request = new LoginRequest { Email = "notfound@example.com", Password = "Password123!" };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials.", result.Error);
        }

        [Fact]
        public async Task LoginAsync_LockedOutAccount_ReturnsFailure()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "locked@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword("SecurePass123!"),
                IsApproved = true,
                LockoutEnd = DateTime.UtcNow.AddMinutes(10)
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new LoginRequest { Email = email, Password = "SecurePass123!" };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("temporarily locked", result.Error);
        }

        [Fact]
        public async Task LoginAsync_InvalidPassword_IncrementsFailedCountAndLocksAfterThreshold()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "failedlock@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword("SecurePass123!"),
                IsApproved = true,
                AccessFailedCount = 4
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new LoginRequest { Email = email, Password = "WrongPassword!" };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials.", result.Error);

            var dbUser = await context.Users.FirstAsync(u => u.Email == email);
            Assert.Equal(5, dbUser.AccessFailedCount);
            Assert.NotNull(dbUser.LockoutEnd);
            Assert.True(dbUser.LockoutEnd > DateTime.UtcNow);
        }

        [Fact]
        public async Task LoginAsync_UnapprovedUser_ReturnsPendingApprovalMessage()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "unapproved@example.com";
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
            var result = await service.LoginAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("pending approval", result.Error);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsSuccessAndToken()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "success@example.com";
            var password = "SecurePassword123!";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword(password),
                IsApproved = true,
                AccessFailedCount = 2
            });
            await context.SaveChangesAsync();

            var request = new LoginRequest { Email = email, Password = password };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.Token);
            Assert.NotNull(result.User);

            var dbUser = await context.Users.FirstAsync(u => u.Email == email);
            Assert.Equal(0, dbUser.AccessFailedCount);
            Assert.Null(dbUser.LockoutEnd);

            var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.UserId == dbUser.Id.ToString() && a.Action == "Login");
            Assert.NotNull(audit);
        }

        [Fact]
        public async Task RegisterAsync_NullUserOrMissingFields_ReturnsFailure()
        {
            // Arrange
            var (_, service) = CreateAuthService();

            // Act
            var resNull = await service.RegisterAsync(null!);
            var resEmpty = await service.RegisterAsync(new User { Email = "", Password = "" });

            // Assert
            Assert.False(resNull.Success);
            Assert.False(resEmpty.Success);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateEmail_ReturnsFailure()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "dup@example.com";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword("SecurePassword123!"),
                IsApproved = true
            });
            await context.SaveChangesAsync();

            var newUser = new User { Email = email, Password = "SecurePassword123!", FirstName = "Test" };

            // Act
            var result = await service.RegisterAsync(newUser);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User already exists", result.Error);
        }

        [Fact]
        public async Task RegisterAsync_WeakPassword_ReturnsFailure()
        {
            // Arrange
            var (_, service) = CreateAuthService();
            var newUser = new User { Email = "weak@example.com", Password = "123", FirstName = "Test" };

            // Act
            var result = await service.RegisterAsync(newUser);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("complexity requirements", result.Error);
        }

        [Fact]
        public async Task RegisterAsync_ValidData_CreatesUnapprovedUserAndSendsVerificationEmail()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "newreg@example.com";
            var newUser = new User
            {
                Email = email,
                Password = "SecurePassword123!",
                FirstName = "Jane",
                LastName = "Doe"
            };

            // Act
            var result = await service.RegisterAsync(newUser);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.User);

            var dbUser = await context.Users.FirstAsync(u => u.Email == email);
            Assert.False(dbUser.IsApproved);
            Assert.False(dbUser.IsEmailVerified);
            Assert.NotEqual("SecurePassword123!", dbUser.Password);

            _mockEmailService.Verify(e => e.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task VerifyEmailAsync_InvalidToken_ReturnsFalse()
        {
            // Arrange
            var (_, service) = CreateAuthService();

            // Act
            var resEmpty = await service.VerifyEmailAsync("");
            var resInvalid = await service.VerifyEmailAsync("bad.token.here");

            // Assert
            Assert.False(resEmpty);
            Assert.False(resInvalid);
        }

        [Fact]
        public async Task LogoutAsync_ValidUserId_LogsAuditEntry()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var userId = Guid.NewGuid().ToString();

            // Act
            await service.LogoutAsync(userId);

            // Assert
            var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.UserId == userId && a.Action == "Logout");
            Assert.NotNull(audit);
        }

        [Fact]
        public async Task InitiatePasswordResetAsync_NonExistentEmail_ReturnsFalse()
        {
            // Arrange
            var (_, service) = CreateAuthService();

            // Act
            var result = await service.InitiatePasswordResetAsync(new ForgotPasswordRequest { Email = "nobody@example.com" });

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CompletePasswordResetAsync_InvalidOrExpiredCode_ReturnsFalse()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "resetexp@example.com";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = "oldhash",
                PasswordResetCode = "654321",
                PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(-1) // Expired
            });
            await context.SaveChangesAsync();

            var reqExpired = new ResetPasswordRequest
            {
                Email = email,
                Code = "654321",
                NewPassword = "NewSecurePassword123!"
            };

            var reqWrongCode = new ResetPasswordRequest
            {
                Email = email,
                Code = "000000",
                NewPassword = "NewSecurePassword123!"
            };

            // Act
            var resExpired = await service.CompletePasswordResetAsync(reqExpired);
            var resWrong = await service.CompletePasswordResetAsync(reqWrongCode);

            // Assert
            Assert.False(resExpired);
            Assert.False(resWrong);
        }

        [Fact]
        public async Task CompletePasswordResetAsync_ValidCode_ResetsPasswordAndClearsCode()
        {
            // Arrange
            var (context, service) = CreateAuthService();
            var email = "resetok@example.com";
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = _passwordHasher.HashPassword("OldPass123!"),
                PasswordResetCode = "999888",
                PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15)
            });
            await context.SaveChangesAsync();

            var req = new ResetPasswordRequest
            {
                Email = email,
                Code = "999888",
                NewPassword = "NewSecurePassword123!"
            };

            // Act
            var result = await service.CompletePasswordResetAsync(req);

            // Assert
            Assert.True(result);

            var dbUser = await context.Users.FirstAsync(u => u.Email == email);
            Assert.Null(dbUser.PasswordResetCode);
            Assert.Null(dbUser.PasswordResetCodeExpiry);
            Assert.True(_passwordHasher.VerifyPassword("NewSecurePassword123!", dbUser.Password));
        }

        [Fact]
        public void PasswordHasher_PBKDF2_And_Legacy_Verification()
        {
            // Arrange
            var hasher = new PasswordHasher();
            var password = "SecurePassword123!";

            // Act & Assert PBKDF2
            var pbkdf2Hash = hasher.HashPassword(password);
            Assert.StartsWith("PBKDF2v1:", pbkdf2Hash);
            Assert.True(hasher.VerifyPassword(password, pbkdf2Hash));
            Assert.False(hasher.VerifyPassword("WrongPassword!", pbkdf2Hash));

            // Act & Assert Legacy SHA256 fallback
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var legacyBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            var legacyBase64 = Convert.ToBase64String(legacyBytes);

            Assert.True(hasher.VerifyPassword(password, legacyBase64));
            Assert.False(hasher.VerifyPassword("WrongPassword!", legacyBase64));
        }

        [Fact]
        public void PasswordHasher_Complexity_Validation()
        {
            // Arrange
            var hasher = new PasswordHasher();

            // Act & Assert
            Assert.False(hasher.IsPasswordComplex(null!));
            Assert.False(hasher.IsPasswordComplex("short"));
            Assert.False(hasher.IsPasswordComplex("alllowercase123"));
            Assert.False(hasher.IsPasswordComplex("ALLUPPERCASE123"));
            Assert.False(hasher.IsPasswordComplex("NoDigitsOrSpecialChars"));
            Assert.True(hasher.IsPasswordComplex("ValidPassword123"));
            Assert.True(hasher.IsPasswordComplex("ValidPass!"));
        }
    }
}
