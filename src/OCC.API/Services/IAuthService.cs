using OCC.Shared.DTOs;
using OCC.Shared.Models;

namespace OCC.API.Services
{
    public interface IAuthService
    {
        Task<(bool Success, string Token, User? User, string Error)> LoginAsync(LoginRequest request);
        Task<(bool Success, User? User, string Error)> RegisterAsync(User user);
        Task<bool> VerifyEmailAsync(string token);
        Task LogoutAsync(string userId);
    }
}
