using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.Features.AuthHub.Models
{
    public partial class LoginRequest : ObservableValidator
    {
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Password is required")]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _rememberMe;

        public void Validate() => ValidateAllProperties();
    }
}
