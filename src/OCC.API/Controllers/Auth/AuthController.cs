using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OCC.API.Hubs;
using OCC.API.Services;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCC.API.Controllers
{
    /// <summary>
    /// API Controller providing authentication endpoints including login, registration, email verification, and password reset workflows.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="authService">The authentication service.</param>
        /// <param name="hubContext">The SignalR notification hub context.</param>
        public AuthController(IAuthService authService, IHubContext<NotificationHub> hubContext)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _hubContext = hubContext;
        }

        /// <summary>
        /// Liveness probe endpoint to check if the Auth API is online.
        /// </summary>
        /// <returns>200 OK status with timestamp and status message.</returns>
        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new { Message = "Auth API is alive", Timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Authenticates a user with email and password credentials.
        /// </summary>
        /// <param name="request">The login request containing email and password.</param>
        /// <returns>200 OK with token and user object if successful; 401 Unauthorized or 403 Forbidden on failure.</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            var (success, token, user, error) = await _authService.LoginAsync(request);

            if (!success)
            {
                if (error.Contains("pending approval") || error.Contains("locked"))
                {
                    return StatusCode(403, error);
                }

                return Unauthorized(error);
            }

            return Ok(new { Token = token, User = user });
        }

        /// <summary>
        /// Logs out the currently authenticated user.
        /// </summary>
        /// <returns>200 OK status upon completion.</returns>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await _authService.LogoutAsync(userId);
            }
            return Ok(new { Message = "Successfully logged out" });
        }

        /// <summary>
        /// Registers a new user account.
        /// </summary>
        /// <param name="user">The user profile data.</param>
        /// <returns>200 OK with created user data if successful; 409 Conflict or 400 BadRequest on failure.</returns>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest("Invalid user registration request.");
            }

            var (success, createdUser, error) = await _authService.RegisterAsync(user);

            if (!success)
            {
                if (error.Contains("already exists"))
                {
                    return Conflict(error);
                }
                return BadRequest(error);
            }

            if (createdUser != null && _hubContext != null)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New User Registration: {createdUser.Email}");
                await _hubContext.Clients.All.SendAsync("EntityUpdate", "User", "Create", createdUser.Id);
            }

            return Ok(createdUser);
        }

        /// <summary>
        /// Verifies a user's email address using a JWT token.
        /// </summary>
        /// <param name="token">The email verification JWT token.</param>
        /// <returns>HTML page indicating email verification success or 400 BadRequest on failure.</returns>
        [HttpGet("verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Verification token is required.");
            }

            var success = await _authService.VerifyEmailAsync(token);

            if (!success)
            {
                return BadRequest("Invalid or expired token.");
            }

            return Content(@"
                <html>
                    <head><title>Email Verified</title></head>
                    <body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h1 style='color: green;'>Email Verified!</h1>
                        <p>Thank you for verifying your email address.</p>
                        <p>You can now close this window and log in to the application.</p>
                    </body>
                </html>", "text/html");
        }

        /// <summary>
        /// Initiates password recovery by sending a 6-digit reset code to the user's email.
        /// </summary>
        /// <param name="request">The forgot password request with the target email.</param>
        /// <returns>200 OK status message if successful; 400 BadRequest on failure.</returns>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Email address is required.");
            }

            var success = await _authService.InitiatePasswordResetAsync(request);
            if (!success)
            {
                return BadRequest("Email address not found or invalid request.");
            }
            return Ok(new { Message = "Reset code has been sent to your email." });
        }

        /// <summary>
        /// Completes the password reset process using a verification code and new password.
        /// </summary>
        /// <param name="request">The reset password request containing email, verification code, and new password.</param>
        /// <returns>200 OK status message if successful; 400 BadRequest on failure.</returns>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest("Email, reset code, and new password are required.");
            }

            var success = await _authService.CompletePasswordResetAsync(request);
            if (!success)
            {
                return BadRequest("Invalid/expired reset code or password mismatch.");
            }
            return Ok(new { Message = "Password has been successfully updated." });
        }
    }
}
