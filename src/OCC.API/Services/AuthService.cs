using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Web;

namespace OCC.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher _passwordHasher;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context,
            IConfiguration configuration,
            PasswordHasher passwordHasher,
            IHubContext<NotificationHub> hubContext,
            IEmailService emailService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _hubContext = hubContext;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<(bool Success, string Token, User? User, string Error)> LoginAsync(LoginRequest request)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request?.Email);
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return (false, string.Empty, null, "Invalid client request");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            bool isCredentialsValid = false;

            if (user != null)
            {
                if (_passwordHasher.VerifyPassword(request.Password, user.Password))
                {
                    isCredentialsValid = true;
                }
            }

            if (!isCredentialsValid || user == null)
            {
                _logger.LogWarning("Login failed: Invalid credentials for user {Email}.", request.Email);

                // Log Failed Login
                if (user != null)
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        UserId = user.Id.ToString(),
                        TableName = "Users",
                        RecordId = request.Email,
                        Action = "Login Failed",
                        Timestamp = DateTime.UtcNow,
                        NewValues = $"{{ \"Reason\": \"Invalid credentials\", \"Email\": \"{request.Email}\" }}"
                    });
                    await _context.SaveChangesAsync();
                }

                return (false, string.Empty, null, "Invalid credentials.");
            }

            if (!user.IsApproved)
            {
                _logger.LogWarning("Login failed: User {Email} is not approved.", request.Email);
                
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

        public async Task<(bool Success, User? User, string Error)> RegisterAsync(User user)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", user?.Email);
            if (user == null)
            {
                return (false, null, "Invalid user data.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                return (false, null, "User already exists");
            }

            user.IsApproved = false;
            user.IsEmailVerified = false;
            user.Password = _passwordHasher.HashPassword(user.Password);
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Registration successful for user {Email} ({Id}). Waiting for approval.", user.Email, user.Id);

            // Notify Admins
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New user registered: {user.FirstName} {user.LastName} ({user.Email}) is waiting for approval.");
            await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Create", user.Id);

            // Send Verification Email
            try 
            {
                var verificationToken = GenerateJwtToken(user, 1);
                var encodedToken = HttpUtility.UrlEncode(verificationToken);
                var request = _httpContextAccessor.HttpContext?.Request;
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
                // Continue, don't fail registration just because email failed (for now)
            }

            return (true, user, string.Empty);
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);

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
                }, out SecurityToken validatedToken);

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
                await _context.SaveChangesAsync();

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
            catch
            {
                return false;
            }
        }

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

        private string GenerateJwtToken(User user, int days = 7)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
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

        public async Task<bool> InitiatePasswordResetAsync(ForgotPasswordRequest request)
        {
            _logger.LogInformation("Password reset request initiated for email: {Email}", request?.Email);
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
                return false;
            }

            // Generate random 6-digit verification code
            var random = new Random();
            var resetCode = random.Next(100000, 999999).ToString();

            user.PasswordResetCode = resetCode;
            user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated reset code for {Email}. Sending email...", request.Email);

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
                _logger.LogError(ex, "Failed to send password reset email to {Email}", request.Email);
                return false;
            }
        }

        public async Task<bool> CompletePasswordResetAsync(ResetPasswordRequest request)
        {
            _logger.LogInformation("Password reset completion attempt for email: {Email}", request?.Email);
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return false;
            }

            if (user.PasswordResetCode != request.Code || user.PasswordResetCodeExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Password reset failed for {Email}: Invalid or expired code.", request.Email);
                return false;
            }

            // Update user password
            user.Password = _passwordHasher.HashPassword(request.NewPassword);
            user.PasswordResetCode = null;
            user.PasswordResetCodeExpiry = null;

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
            _logger.LogInformation("Password successfully reset for user: {Email}", request.Email);
            return true;
        }
    }
}
