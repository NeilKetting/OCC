using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class ProfileViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;

        [ObservableProperty]
        private User _user;

        [ObservableProperty]
        private string _oldPassword = string.Empty;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private bool _isChangingPassword;

        public ProfileViewModel(IAuthService authService, IToastService toastService)
        {
            _authService = authService;
            _toastService = toastService;
            
            // Clone the current user to avoid direct modification before saving
            var current = _authService.CurrentUser;
            if (current != null)
            {
                _user = new User
                {
                    Id = current.Id,
                    FirstName = current.FirstName,
                    LastName = current.LastName,
                    Email = current.Email,
                    Phone = current.Phone,
                    Location = current.Location,
                    ProfilePictureBase64 = current.ProfilePictureBase64,
                    UserRole = current.UserRole,
                    Branch = current.Branch,
                    PublicKey = current.PublicKey,
                    Permissions = current.Permissions
                };
            }
            else
            {
                _user = new User();
            }
        }

        [RelayCommand]
        private async Task SaveProfile()
        {
            IsBusy = true;
            BusyText = "Saving profile...";
            
            try
            {
                var success = await _authService.UpdateProfileAsync(User);
                if (success)
                {
                    _toastService.ShowSuccess("Success", "Profile updated successfully");
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to update profile");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ChangePassword()
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
            {
                _toastService.ShowError("Error", "Passwords do not match or are empty");
                return;
            }

            IsBusy = true;
            BusyText = "Changing password...";
            
            try
            {
                var success = await _authService.ChangePasswordAsync(OldPassword, NewPassword);
                if (success)
                {
                    _toastService.ShowSuccess("Success", "Password changed successfully");
                    OldPassword = string.Empty;
                    NewPassword = string.Empty;
                    ConfirmPassword = string.Empty;
                    IsChangingPassword = false;
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to change password. Verify your current password.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void TogglePasswordChange()
        {
            IsChangingPassword = !IsChangingPassword;
        }

        [RelayCommand]
        private void UploadProfilePicture()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
                Title = "Select Profile Picture"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(dialog.FileName);
                    User.ProfilePictureBase64 = Convert.ToBase64String(bytes);
                    OnPropertyChanged(nameof(User));
                }
                catch (Exception ex)
                {
                    _toastService.ShowError("Error", $"Failed to load image: {ex.Message}");
                }
            }
        }
    }
}
