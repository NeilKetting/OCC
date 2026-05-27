using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    /// <summary>
    /// ViewModel for managing the user profile, saving changes, uploading a profile picture,
    /// and updating user passwords.
    /// </summary>
    public partial class ProfileViewModel : ViewModelBase
    {
        #region Private Fields

        // Manages user authentication and profiles
        private readonly IAuthService _authService;

        // Shows toast notifications
        private readonly IToastService _toastService;

        // Displays standard system file and alert dialogs
        private readonly IDialogService _dialogService;

        #endregion

        #region Properties & Observables

        // Clone of the currently logged-in user profile details for editing
        [ObservableProperty]
        private User _user;

        // Current password field input for changing passwords
        [ObservableProperty]
        private string _oldPassword = string.Empty;

        // New password field input
        [ObservableProperty]
        private string _newPassword = string.Empty;

        // Confirmation of the new password field input
        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        // Toggles visibility of the change password controls overlay
        [ObservableProperty]
        private bool _isChangingPassword;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the user profile view model and clones the logged-in user context.
        /// </summary>
        public ProfileViewModel(IAuthService authService, IToastService toastService, IDialogService dialogService)
        {
            _authService = authService;
            _toastService = toastService;
            _dialogService = dialogService;
            
            // Clone the current user to avoid direct modification before saving
            var current = _authService.CurrentUser;
            if (current != null)
            {
                _user = new User
                {
                    Id = current.Id,
                    Email = current.Email,
                    Password = current.Password,
                    FirstName = current.FirstName,
                    LastName = current.LastName,
                    Phone = current.Phone,
                    CompanyName = current.CompanyName,
                    Location = current.Location,
                    ProfilePictureBase64 = current.ProfilePictureBase64,
                    ApproverId = current.ApproverId,
                    IsApproved = current.IsApproved,
                    IsEmailVerified = current.IsEmailVerified,
                    Permissions = current.Permissions,
                    PublicKey = current.PublicKey,
                    ProvisionalPrivateKey = current.ProvisionalPrivateKey,
                    Branch = current.Branch,
                    UserRole = current.UserRole,
                    CreatedAtUtc = current.CreatedAtUtc,
                    CreatedBy = current.CreatedBy,
                    UpdatedAtUtc = current.UpdatedAtUtc,
                    UpdatedBy = current.UpdatedBy,
                    IsActive = current.IsActive,
                    RowVersion = current.RowVersion
                };
            }
            else
            {
                _user = new User();
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Saves the updated profile data via the AuthService.
        /// </summary>
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

        /// <summary>
        /// Validates input and triggers user password change on the backend.
        /// </summary>
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

        /// <summary>
        /// Toggles display of password modification panel inputs.
        /// </summary>
        [RelayCommand]
        private void TogglePasswordChange()
        {
            IsChangingPassword = !IsChangingPassword;
        }

        /// <summary>
        /// Prompts user to select a picture file and uploads it as Base64.
        /// </summary>
        [RelayCommand]
        private void UploadProfilePicture()
        {
            var filePath = _dialogService.ShowOpenFileDialog("Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp", "Select Profile Picture");
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(filePath);
                    User.ProfilePictureBase64 = Convert.ToBase64String(bytes);
                    OnPropertyChanged(nameof(User));
                }
                catch (Exception ex)
                {
                    _toastService.ShowError("Error", $"Failed to load image: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
