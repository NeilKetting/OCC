using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.Features.AuthHub.Models
{
    public partial class RegisterRequest : ObservableValidator
    {
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "First name is required")]
        private string _firstName = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Last name is required")]
        private string _lastName = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        private string _password = string.Empty;

        [ObservableProperty]
        [property: Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        private string _confirmPassword = string.Empty;

        public void Validate() => ValidateAllProperties();
    }
}
