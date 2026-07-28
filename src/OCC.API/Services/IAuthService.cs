using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.API.Services
{
    /// <summary>
    /// Contract for authentication, registration, identity management, and password lifecycle operations.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates a user with email and password credentials.
        /// Performs account approval verification and rate limiting / lockout checks.
        /// </summary>
        /// <param name="request">The login request payload containing credentials.</param>
        /// <returns>A tuple indicating success status, generated JWT token, user entity, and error message if applicable.</returns>
        Task<(bool Success, string Token, User? User, string Error)> LoginAsync(LoginRequest request);

        /// <summary>
        /// Registers a new user in the system with hashed credentials and triggers verification email and admin notifications.
        /// </summary>
        /// <param name="user">The user entity to register.</param>
        /// <returns>A tuple indicating success status, created user entity, and error message if applicable.</returns>
        Task<(bool Success, User? User, string Error)> RegisterAsync(User user);

        /// <summary>
        /// Verifies a user's email address using a signed JWT verification token.
        /// </summary>
        /// <param name="token">The JWT verification token.</param>
        /// <returns><c>true</c> if the token is valid and email is verified; otherwise, <c>false</c>.</returns>
        Task<bool> VerifyEmailAsync(string token);

        /// <summary>
        /// Logs out a user and records an audit trail event.
        /// </summary>
        /// <param name="userId">The ID of the user logging out.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LogoutAsync(string userId);

        /// <summary>
        /// Initiates a password reset flow by generating a 6-digit verification code and emailing it to the user.
        /// </summary>
        /// <param name="request">The forgot password request containing the target email address.</param>
        /// <returns><c>true</c> if the reset code was generated and sent; otherwise, <c>false</c>.</returns>
        Task<bool> InitiatePasswordResetAsync(ForgotPasswordRequest request);

        /// <summary>
        /// Completes a password reset operation by validating the verification code and setting a new hashed password.
        /// </summary>
        /// <param name="request">The reset password request containing email, verification code, and new password.</param>
        /// <returns><c>true</c> if the password was successfully updated; otherwise, <c>false</c>.</returns>
        Task<bool> CompletePasswordResetAsync(ResetPasswordRequest request);
    }
}
