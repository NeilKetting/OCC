using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using System.ComponentModel.DataAnnotations;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class UserDetailViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IRepository<User> _userRepository;
        private readonly Microsoft.Extensions.Logging.ILogger<UserDetailViewModel> _logger;
        private readonly IDialogService _dialogService;
        private Guid? _existingUserId;

        #endregion

        #region Events

        public event EventHandler? CloseRequested;
        public event EventHandler? UserSaved;

        #endregion

        #region Observables

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [Required(ErrorMessage = "First name is required")]
        private string _firstName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [Required(ErrorMessage = "Last name is required")]
        private string _lastName = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty; // Only used for new users or resets

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _location = string.Empty;

        [ObservableProperty]
        private UserRole _selectedRole = UserRole.Guest;

        [ObservableProperty]
        private bool _isApproved;

        [ObservableProperty]
        private bool _isEmailVerified;

        [ObservableProperty]
        private string _saveButtonText = "Create User";



        public new string Title
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DisplayName))
                {
                    return _existingUserId.HasValue ? "Edit User" : "Create User";
                }
                return DisplayName;
            }
        }



        [ObservableProperty]
        private bool _canAccessOrders;

        [ObservableProperty]
        private bool _canAccessInventoryOnly;

        [ObservableProperty]
        private bool _canCreateProjects;

        #endregion

        #region Properties

        public UserRole[] UserRoles { get; } = Enum.GetValues<UserRole>();

        public string DisplayName => $"{FirstName} {LastName}".Trim();

        #endregion

        #region Constructors

        public UserDetailViewModel(IRepository<User> userRepository, Microsoft.Extensions.Logging.ILogger<UserDetailViewModel> logger, IDialogService dialogService)
        {
            _userRepository = userRepository;
            _logger = logger;
            _dialogService = dialogService;
        }

        public UserDetailViewModel()
        {
            // Designer constructor
            _userRepository = null!;
            _logger = null!;
            _dialogService = null!;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task Save()
        {
            try
            {
                ValidateAllProperties();
                if (HasErrors) return;

                BusyText = "Saving user...";

                User user;

                if (_existingUserId.HasValue)
                {
                    // Update
                    user = await _userRepository.GetByIdAsync(_existingUserId.Value) ?? new User { Id = _existingUserId.Value };
                }
                else
                {
                    // Create
                    user = new User();
                    // For new users, set password if provided, else maybe default?
                    // Real app would handle this better (hashing etc).
                    if (!string.IsNullOrEmpty(Password))
                    {
                        user.Password = Password; 
                    }
                }

                user.FirstName = FirstName;
                user.LastName = LastName;
                user.Email = Email;
                user.Phone = Phone;
                user.Location = Location;
                user.UserRole = SelectedRole;
                user.IsApproved = IsApproved;
                user.IsEmailVerified = IsEmailVerified;

                // Save Permissions
                var permsList = new System.Collections.Generic.List<string>();
                if (CanAccessOrders) permsList.Add(Infrastructure.NavigationRoutes.Feature_OrderManagement);
                if (CanAccessInventoryOnly) permsList.Add(Infrastructure.NavigationRoutes.Feature_OrderInventoryOnly);
                if (CanCreateProjects) permsList.Add(Infrastructure.NavigationRoutes.Feature_ProjectCreation);
                
                user.Permissions = string.Join(",", permsList);

                if (_existingUserId.HasValue)
                {
                     // If updating and password field is not empty, update it
                    if (!string.IsNullOrEmpty(Password))
                    {
                        user.Password = Password;
                    }
                    await _userRepository.UpdateAsync(user);
                }
                else
                {
                    await _userRepository.AddAsync(user);
                }

                UserSaved?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OCC.Client.Infrastructure.Exceptions.ConcurrencyException cex)
            {
                await _dialogService.ShowAlertAsync("Concurrency Conflict", cex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user");
                await _dialogService.ShowAlertAsync("Error", $"Failed to save user: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        public void Load(User user)
        {
            if (user == null) return;

            _existingUserId = user.Id;
            SaveButtonText = "Save Changes";

            FirstName = user.FirstName;
            LastName = user.LastName;
            Email = user.Email;
            Phone = user.Phone ?? string.Empty;
            Location = user.Location ?? string.Empty;
            SelectedRole = user.UserRole;
            IsApproved = user.IsApproved;
            IsEmailVerified = user.IsEmailVerified;

            // Load Permissions
            var perms = (user.Permissions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            CanAccessOrders = perms.Contains(Infrastructure.NavigationRoutes.Feature_OrderManagement);
            CanAccessInventoryOnly = perms.Contains(Infrastructure.NavigationRoutes.Feature_OrderInventoryOnly);
            CanCreateProjects = perms.Contains(Infrastructure.NavigationRoutes.Feature_ProjectCreation);
            
            // We typically don't load the password back into the UI
            Password = "";

            OnPropertyChanged(nameof(DisplayName));
        }

        partial void OnFirstNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
        partial void OnLastNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));

        #endregion
    }
}
