using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using System.IO;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace OCC.Client.ViewModels.Shared
{
    public partial class ProfileViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IRepository<Project> _projectRepository;

        #endregion

        #region Events

        public event EventHandler? CloseRequested;
        public event EventHandler? ChangeEmailRequested;

        #endregion

        #region Observables

        [ObservableProperty]
        private string _firstName = string.Empty;

        [ObservableProperty]
        private string _lastName = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _initials = string.Empty;

        [ObservableProperty]
        private string _location = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private UserRole _selectedRole;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap? _profilePictureBitmap;

        [ObservableProperty]
        private string? _profilePictureBase64;

        [ObservableProperty]
        private bool _isChangingPassword;

        [ObservableProperty]
        private string _oldPassword = string.Empty;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;
        
        #endregion

        #region Properties

        public ObservableCollection<UserRole> Roles { get; } = new(Enum.GetValues<UserRole>());

        #endregion

        #region Constructors

        public ProfileViewModel(IAuthService authService, 
                                IRepository<Project> projectRepository,
                                IDialogService dialogService)
        {
            _authService = authService;
            _projectRepository = projectRepository;
            _dialogService = dialogService;
            // Assuming we'd fetch actual data here
            LoadUserData();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void EditEmail() 
        {
             // Trigger popup via event or messenger
             ChangeEmailRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ToggleChangePassword()
        {
             IsChangingPassword = !IsChangingPassword;
             OldPassword = string.Empty;
             NewPassword = string.Empty;
             ConfirmPassword = string.Empty;
        }

        [RelayCommand]
        private async Task ChangePassword()
        {
            if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword))
            {
                await _dialogService.ShowAlertAsync("Error", "Please fill in all password fields.");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                 await _dialogService.ShowAlertAsync("Error", "New passwords do not match.");
                 return;
            }

            var success = await _authService.ChangePasswordAsync(OldPassword, NewPassword);
            if (success)
            {
                await _dialogService.ShowAlertAsync("Success", "Password changed successfully.");
                IsChangingPassword = false;
            }
            else
            {
                await _dialogService.ShowAlertAsync("Error", "Failed to change password. Please check your old password.");
            }
        }

        [RelayCommand]
        private async Task UploadPicture()
        {
             try
            {
                // In a real generic ViewModel we might need a file picker service abstraction, 
                // but usually we can pass the TopLevel storage provider or similar.
                // For now, assuming we use a service or we rely on the View to call this with a result.
                // But typically ViewModels don't open dialogs directly without a service.
                // Let's assume IDialogService has OpenFilePickerAsync, or we add a command parameter
                // BUT current IDialogService is simple. 
                // We'll use a known pattern: The View will handle the file picking and just set property or call a method?
                // actually, let's implement a simple file picker logic here using parameter if passed, 
                // OR better, let's just make IDialogService handle it if possible.
                // Re-checking IDialogService... it doesn't have file picker.
                // We'll trust the View to handle the storage provider interaction and pass the file path/stream here?
                // Or better, stick to our pattern of services. 
                // Let's assume we simply want to simulate the logic or use a command that the View binds to and provides the StorageProvider.
                // For now, let's just create a method 'SetProfilePicture(Stream stream)' that the View calls.
                // Actually, let's simply assume the user will implement the service later or we do it now.
                // Simplest: The View code-behind handles the interaction and calls a method on VM.
            }
            catch (Exception ex)
            {
                 await _dialogService.ShowAlertAsync("Error", $"Image load failed: {ex.Message}");
            }
        }
        
        // This method will be called by the View's code-behind after picking a file
        public async Task SetProfilePictureAsync(Stream stream)
        {
             using var memoryStream = new MemoryStream();
             await stream.CopyToAsync(memoryStream);
             memoryStream.Position = 0;
             
             // Create Bitmap
             ProfilePictureBitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
             
             // Convert to Base64
             ProfilePictureBase64 = Convert.ToBase64String(memoryStream.ToArray());
        }

        [RelayCommand]
        private async Task Save()
        {
            if (_authService.CurrentUser != null)
            {
                 _authService.CurrentUser.Location = Location;
                 _authService.CurrentUser.Phone = Phone;
                 // UserRole is not editable by User, so we don't save it back if it's disabled in UI
                 // but we update the property just in case admin logic allows it later
                 
                 if (!string.IsNullOrEmpty(ProfilePictureBase64))
                 {
                     _authService.CurrentUser.ProfilePictureBase64 = ProfilePictureBase64;
                 }

                 var success = await _authService.UpdateProfileAsync(_authService.CurrentUser);
                 
                 if (success)
                 {
                     // await _dialogService.ShowAlertAsync("Success", "Profile updated successfully.");
                     CloseRequested?.Invoke(this, EventArgs.Empty);
                     // We'll close using the Messaging or Event
                     // Using Messenger is better but we use event here
                 }
                 else
                 {
                     await _dialogService.ShowAlertAsync("Error", "Failed to update profile.");
                     return; 
                 }
            }
             CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        private void LoadUserData()
        {
            var user = _authService.CurrentUser;
            if (user != null)
            {
                FirstName = user.FirstName;
                LastName = user.LastName;
                DisplayName = user.DisplayName ?? string.Empty;
                Email = user.Email;
                Initials = GetInitials(user.DisplayName);
                Phone = user.Phone ?? "";
                Location = user.Location ?? "";
                SelectedRole = user.UserRole;
                
                if (!string.IsNullOrEmpty(user.ProfilePictureBase64))
                {
                    try 
                    {
                        var bytes = Convert.FromBase64String(user.ProfilePictureBase64);
                        using var ms = new MemoryStream(bytes);
                        ProfilePictureBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                        ProfilePictureBase64 = user.ProfilePictureBase64;
                    }
                    catch
                    {
                        // Invalid image data, ignore
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private string GetInitials(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "U";
            var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        #endregion
    }
}
