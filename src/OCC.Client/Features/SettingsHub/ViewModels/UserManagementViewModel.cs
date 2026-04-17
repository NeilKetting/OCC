using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class UserManagementViewModel : ViewModelBase, CommunityToolkit.Mvvm.Messaging.IRecipient<OCC.Client.ViewModels.Messages.EntityUpdatedMessage>
    {
        #region Private Members

        private readonly IRepository<User> _userRepository;
        private readonly IDialogService _dialogService;
        private readonly Microsoft.Extensions.Logging.ILogger<UserDetailViewModel> _userDetailLogger;
        private List<User> _allUsers = new();

        #endregion

        #region Observables

        [ObservableProperty]
        private string _activeFilter = "All";

        [ObservableProperty]
        private int _totalUsers = 0;

        [ObservableProperty]
        private int _pendingApprovalCount = 0;

        [ObservableProperty]
        private int _adminCount = 0;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private bool _isUserPopupVisible;

        [ObservableProperty]
        private UserDetailViewModel? _userPopup;

        [ObservableProperty]
        private User? _selectedUser;

        #endregion

        #region Constructors

        public UserManagementViewModel()
        {
            // Designer support
            _userRepository = null!;
            _dialogService = null!;
            _userDetailLogger = null!;
        }

        public UserManagementViewModel(IRepository<User> userRepository, IDialogService dialogService, Microsoft.Extensions.Logging.ILogger<UserDetailViewModel> userDetailLogger)
        {
            _userRepository = userRepository;
            _dialogService = dialogService;
            _userDetailLogger = userDetailLogger;
            _ = LoadData();
            
            CommunityToolkit.Mvvm.Messaging.IMessengerExtensions.RegisterAll(CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default, this);
        }

        public void Receive(OCC.Client.ViewModels.Messages.EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "User")
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => LoadData());
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void AddUser()
        {
            UserPopup = new UserDetailViewModel(_userRepository, _userDetailLogger, _dialogService);
            UserPopup.CloseRequested += (s, e) => IsUserPopupVisible = false;
            UserPopup.UserSaved += async (s, e) => 
            {
                IsUserPopupVisible = false;
                await LoadData();
            };
            IsUserPopupVisible = true;
        }

        [RelayCommand]
        public void EditUser(User user)
        {
            if (user == null) return;

            UserPopup = new UserDetailViewModel(_userRepository, _userDetailLogger, _dialogService);
            UserPopup.Load(user);
            UserPopup.CloseRequested += (s, e) => IsUserPopupVisible = false;
            UserPopup.UserSaved += async (s, e) => 
            {
                IsUserPopupVisible = false;
                await LoadData(); 
            };
            IsUserPopupVisible = true;
        }

        [RelayCommand]
        public async Task DeleteUser(User user)
        {
            if (user == null) return;

            try
            {
                var result = await _dialogService.ShowConfirmationAsync("Delete User", $"Are you sure you want to delete {user.FirstName} {user.LastName}?");
                if (result)
                {
                    try
                    {
                        var email = user.Email?.ToLowerInvariant();
                        if (email == "neil@mdk.co.za" || email == "neil@origize63.co.za")
                        {
                            await _dialogService.ShowAlertAsync("Restricted Action", "The Developer account cannot be deleted.");
                            return;
                        }

                        BusyText = "Deleting user...";
                        IsBusy = true;
                        await _userRepository.DeleteAsync(user.Id);
                await LoadData();
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[UserManagementViewModel] Error deleting user: {ex.Message}");
                 if (_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Failed to delete user: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ApproveUser(User user)
        {
            if (user == null) return;
            try
            {
                BusyText = "Approving user...";
                IsBusy = true;
                user.IsApproved = true;
                await _userRepository.UpdateAsync(user);
                await LoadData(); // Refresh counts
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[UserManagementViewModel] Error approving user: {ex.Message}");
                 if (_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Failed to approve user: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SetFilter(string filter)
        {
            ActiveFilter = filter;
            FilterUsers();
        }

        #endregion

        #region Methods



        public async void OpenUser(Guid userId)
        {
            if (_allUsers.Count == 0)
            {
                await LoadData();
            }

            var user = _allUsers.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                EditUser(user);
            }
        }

        public async Task LoadData()
        {
            try
            {
                BusyText = "Loading users...";
                IsBusy = true;
                var users = await _userRepository.GetAllAsync();
                // Sort by Name
                _allUsers = users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
                
                TotalUsers = _allUsers.Count;
                PendingApprovalCount = _allUsers.Count(u => !u.IsApproved);
                AdminCount = _allUsers.Count(u => u.UserRole == UserRole.Admin);

                FilterUsers();
                OnPropertyChanged(nameof(TotalUsers));
                OnPropertyChanged(nameof(PendingApprovalCount));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading users: {ex}");
                if (_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Critical Error loading users: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchQueryChanged(string value) => FilterUsers();

        private void FilterUsers()
        {
            var filtered = _allUsers.AsEnumerable();

            // Text search
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(u => 
                    u.FirstName.ToLower().Contains(q) || 
                    u.LastName.ToLower().Contains(q) || 
                    u.Email.ToLower().Contains(q));
            }

            // Category filter
            switch (ActiveFilter)
            {
                case "Pending":
                    filtered = filtered.Where(u => !u.IsApproved);
                    break;
                case "Admins":
                    filtered = filtered.Where(u => u.UserRole == UserRole.Admin);
                    break;
                case "All":
                default:
                    break;
            }

            Users = new ObservableCollection<User>(filtered);
        }

        #endregion
    }
}
