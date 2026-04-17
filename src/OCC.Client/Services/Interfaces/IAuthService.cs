using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string ErrorMessage)> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(User user);
        Task LogoutAsync();
        Task<bool> UpdateProfileAsync(User user);
        Task<bool> ChangePasswordAsync(string oldPassword, string newPassword);
        User? CurrentUser { get; }
        string? AuthToken { get; }
        bool IsAuthenticated { get; }
    }
}
