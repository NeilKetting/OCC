using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IAuthService
    {
        event EventHandler? UserChanged;
        Task<(bool Success, string ErrorMessage)> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(User user);
        Task LogoutAsync();
        Task<bool> UpdateProfileAsync(User user);
        Task<bool> ChangePasswordAsync(string oldPassword, string newPassword);
        User? CurrentUser { get; }
        string? CurrentToken { get; }
        bool IsAuthenticated { get; }
    }
}
