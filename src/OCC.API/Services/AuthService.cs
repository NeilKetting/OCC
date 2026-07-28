using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Web;
using System.Security.Cryptography;

namespace OCC.API.Services
{
    /// <summary>
    /// Implements OWASP-compliant identity, authentication, user registration, and password recovery services.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;

        private const int MaxFailedAccessAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthService"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="passwordHasher">The password hashing service implementation.</param>
        /// <param name="hubContext">The SignalR notification hub context.</param>
        /// <param name="emailService">The email delivery service.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="logger">The logger instance.</param>
        public AuthService(
            AppDbContext context,
            IConfiguration configuration,
            IPasswordHasher passwordHasher,
            IHubContext<NotificationHub> hubContext,
            IEmailService emailService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _hubContext = hubContext;
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _httpContextAccessor = httpContextAccessor;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Authenticates a user with email and password credentials.
        /// Performs account approval verification and rate limiting / lockout checks.
        /// </summary>
        /// <param name="request">The login request payload containing credentials.</param>
        /// <returns>A tuple indicating success status, generated JWT token, user entity, and error message if applicable.</returns>
        public async Task<(bool Success, string Token, User? User, string Error)> LoginAsync(LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return (false, string.Empty, null, "Invalid client request");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            _logger.LogInformation("Login attempt for email: {Email}", normalizedEmail);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null)
            {
                _logger.LogWarning("Login failed: User {Email} not found.", normalizedEmail);
                return (false, string.Empty, null, "Invalid credentials.");
            }

            // Check if account is locked out
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                _logger.LogWarning("Login blocked: Account for user {Email} is currently locked out.", normalizedEmail);
                return (false, string.Empty, null, "Account is temporarily locked due to multiple failed login attempts. Please try again later.");
            }

            bool isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.Password);

            if (!isPasswordValid)
            {
                _logger.LogWarning("Login failed: Invalid password for user {Email}.", normalizedEmail);

                // Increment failed login attempt counter and lock if threshold exceeded
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= MaxFailedAccessAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                    _logger.LogWarning("Account locked for user {Email} after {Count} failed attempts.", normalizedEmail, user.AccessFailedCount);
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = user.Id.ToString(),
                    TableName = "Users",
                    RecordId = user.Id.ToString(),
                    Action = "Login Failed",
                    Timestamp = DateTime.UtcNow,
                    NewValues = $"{{ \"Reason\": \"Invalid credentials\", \"FailedAttempts\": {user.AccessFailedCount} }}"
                });

                await _context.SaveChangesAsync();
                return (false, string.Empty, null, "Invalid credentials.");
            }

            if (!user.IsApproved)
            {
                _logger.LogWarning("Login failed: Account for user {Email} is pending administrator approval.", normalizedEmail);

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = user.Id.ToString(),
                    TableName = "Users",
                    RecordId = user.Id.ToString(),
                    Action = "Login Blocked",
                    Timestamp = DateTime.UtcNow,
                    NewValues = "{ \"Reason\": \"Account not approved\" }"
                });
                await _context.SaveChangesAsync();

                return (false, string.Empty, null, "Account pending approval. Please wait for an administrator to activate your account.");
            }

            // Reset failed login counter and lockout upon successful authentication
            if (user.AccessFailedCount > 0 || user.LockoutEnd.HasValue)
            {
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;
            }

            var tokenString = GenerateJwtToken(user);

            _logger.LogInformation("Login successful for user {Email} ({Id})", user.Email, user.Id);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id.ToString(),
                TableName = "Users",
                RecordId = user.Id.ToString(),
                Action = "Login",
                Timestamp = DateTime.UtcNow,
                NewValues = "{ \"Action\": \"User Logged In\" }"
            });
            await _context.SaveChangesAsync();

            return (true, tokenString, user, string.Empty);
        }

        /// <summary>
        /// Registers a new user in the system with hashed credentials and triggers verification email and admin notifications.
        /// </summary>
        /// <param name="user">The user entity to register.</param>
        /// <returns>A tuple indicating success status, created user entity, and error message if applicable.</returns>
        public async Task<(bool Success, User? User, string Error)> RegisterAsync(User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
            {
                return (false, null, "Invalid user data.");
            }

            var normalizedEmail = user.Email.Trim().ToLowerInvariant();
            _logger.LogInformation("Registration attempt for email: {Email}", normalizedEmail);

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                return (false, null, "User already exists");
            }

            if (!_passwordHasher.IsPasswordComplex(user.Password))
            {
                return (false, null, "Password does not meet OWASP complexity requirements. It must be at least 8 characters long and contain uppercase, lowercase, and a digit or special character.");
            }

            user.Email = normalizedEmail;
            user.IsApproved = false;
            user.IsEmailVerified = false;
            user.Password = _passwordHasher.HashPassword(user.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Registration successful for user {Email} ({Id}). Waiting for approval.", user.Email, user.Id);

            if (_hubContext != null)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New user registered: {user.FirstName} {user.LastName} ({user.Email}) is waiting for approval.");
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Create", user.Id);
            }

            // Send Verification Email
            try
            {
                var verificationToken = GenerateJwtToken(user, 1);
                var encodedToken = HttpUtility.UrlEncode(verificationToken);
                var request = _httpContextAccessor?.HttpContext?.Request;
                var baseUri = request != null ? $"{request.Scheme}://{request.Host}" : "https://api.origize63.co.za";
                var verifyLink = $"{baseUri}/api/Auth/verify?token={encodedToken}";

                var emailBody = $@"
                    <p>Hi {user.FirstName},</p>
                    <p>Welcome to Orange Circle Construction! Please verify your email address to complete your registration.</p>
                    <a href='{verifyLink}' class='button'>Verify Email</a>
                    <p>If the button doesn't work, copy and paste this link:</p>
                    <p>{verifyLink}</p>
                ";

                await _emailService.SendEmailAsync(user.Email, "Verify Your Email Address", emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email to {Email}", user.Email);
            }

            return (true, user, string.Empty);
        }

        /// <summary>
        /// Verifies a user's email address using a signed JWT verification token.
        /// </summary>
        /// <param name="token">The JWT verification token.</param>
        /// <returns><c>true</c> if the token is valid and email is verified; otherwise, <c>false</c>.</returns>
        public async Task<bool> VerifyEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            var handler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey)) return false;

            var key = Encoding.UTF8.GetBytes(jwtKey);

            try
            {
                var claimsPrincipal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true
                }, out _);

                var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.Name);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return false;
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.IsEmailVerified = true;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = user.Id.ToString(),
                    TableName = "Users",
                    RecordId = user.Id.ToString(),
                    Action = "Email Verified",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed email verification token validation.");
                return false;
            }
        }

        /// <summary>
        /// Logs out a user and records an audit trail event.
        /// </summary>
        /// <param name="userId">The ID of the user logging out.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LogoutAsync(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                _logger.LogInformation("Logout for user {UserId}", userId);
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    TableName = "Users",
                    RecordId = userId,
                    Action = "Logout",
                    Timestamp = DateTime.UtcNow,
                    NewValues = "{ \"Action\": \"User Logged Out\" }"
                });
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Initiates a password reset flow by generating a 6-digit verification code and emailing it to the user.
        /// </summary>
        /// <param name="request">The forgot password request containing the target email address.</param>
        /// <returns><c>true</c> if the reset code was generated and sent; otherwise, <c>false</c>.</returns>
        public async Task<bool> InitiatePasswordResetAsync(ForgotPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return false;
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            _logger.LogInformation("Password reset request initiated for email: {Email}", normalizedEmail);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent email: {Email}", normalizedEmail);
                return false;
            }

            // Generate cryptographically random 6-digit verification code
            var resetCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");

            user.PasswordResetCode = resetCode;
            user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated reset code for {Email}. Sending email...", normalizedEmail);

            try
            {
                var subject = "Reset Your Password - Orange Circle Construction";
                var body = $@"
                    <p>Hi {user.FirstName},</p>
                    <p>You requested to reset your password. Use the following code to complete the process:</p>
                    <h2 style='color:#f39c12; font-size:24px; letter-spacing: 2px;'>{resetCode}</h2>
                    <p>This code is valid for 15 minutes. If you did not request this, you can safely ignore this email.</p>
                ";

                await _emailService.SendEmailAsync(user.Email, subject, body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                return false;
            }
        }

        /// <summary>
        /// Completes a password reset operation by validating the verification code and setting a new hashed password.
        /// </summary>
        /// <param name="request">The reset password request containing email, verification code, and new password.</param>
        /// <returns><c>true</c> if the password was successfully updated; otherwise, <c>false</c>.</returns>
        public async Task<bool> CompletePasswordResetAsync(ResetPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return false;
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            _logger.LogInformation("Password reset completion attempt for email: {Email}", normalizedEmail);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user == null)
            {
                return false;
            }

            if (user.PasswordResetCode != request.Code || !user.PasswordResetCodeExpiry.HasValue || user.PasswordResetCodeExpiry.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Password reset failed for {Email}: Invalid or expired code.", normalizedEmail);
                return false;
            }

            if (!_passwordHasher.IsPasswordComplex(request.NewPassword))
            {
                _logger.LogWarning("Password reset failed for {Email}: Password complexity requirement not met.", normalizedEmail);
                return false;
            }

            // Update user password and clear reset tokens
            user.Password = _passwordHasher.HashPassword(request.NewPassword);
            user.PasswordResetCode = null;
            user.PasswordResetCodeExpiry = null;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            _context.Entry(user).State = EntityState.Modified;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id.ToString(),
                TableName = "Users",
                RecordId = user.Id.ToString(),
                Action = "Password Reset Complete",
                Timestamp = DateTime.UtcNow,
                NewValues = "{ \"Action\": \"Password reset successfully via reset code\" }"
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Password successfully reset for user: {Email}", normalizedEmail);
            return true;
        }

        /// <summary>
        /// Generates a signed JWT authentication or verification token for a user.
        /// </summary>
        /// <param name="user">The target user entity.</param>
        /// <param name="days">The token validity duration in days.</param>
        /// <returns>A signed JWT token string.</returns>
        private string GenerateJwtToken(User user, int days = 7)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"] ?? "SuperSecretDefaultTestingKeyWithMinimumLength32Bytes!";
            var key = Encoding.UTF8.GetBytes(jwtKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.UserRole.ToString()),
                    new Claim(ClaimTypes.GivenName, user.DisplayName ?? user.Email)
                }),
                Expires = DateTime.UtcNow.AddDays(days),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
