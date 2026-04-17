using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.Admin.Users.ViewModels
{
    public partial class UserListViewModel : OverlayHostViewModel
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<UserListViewModel> _logger;
        private List<User> _allUsers = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _pendingApprovalCount;
        [ObservableProperty] private int _adminCount;

        [ObservableProperty] private User? _selectedUser;

        public UserListViewModel(
            IUserService userService, 
            IAuthService authService,
            IDialogService dialogService,
            ILogger<UserListViewModel> logger)
        {
            _userService = userService;
            _authService = authService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "User Management";
            
            _ = LoadData();
        }

        [RelayCommand]
        public async Task LoadData()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading users...";
                
                var users = await _userService.GetUsersAsync();
                _allUsers = users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
                
                FilterUsers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void AddUser()
        {
            var user = new User();
            OpenOverlay(new UserDetailViewModel(this, user, _userService, _dialogService, _logger));
        }

        [RelayCommand]
        public void EditUser(User? user)
        {
            if (user == null) return;
            OpenOverlay(new UserDetailViewModel(this, user, _userService, _dialogService, _logger));
        }

        [RelayCommand]
        private async Task DeleteUser(User? user)
        {
            if (user == null) return;
            
            // Assume confirmation for now as per image '...' menu usually has Delete
            try
            {
                IsBusy = true;
                BusyText = "Deleting user...";
                var success = await _userService.DeleteUserAsync(user.Id);
                if (success)
                {
                    await LoadData();
                }
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

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(u => 
                    (u.FirstName?.ToLower().Contains(query) ?? false) ||
                    (u.LastName?.ToLower().Contains(query) ?? false) ||
                    (u.Email?.ToLower().Contains(query) ?? false));
            }

            var result = filtered.ToList();
            Users = new ObservableCollection<User>(result);

            // Update Stats
            TotalCount = _allUsers.Count;
            PendingApprovalCount = _allUsers.Count(u => !u.IsApproved);
            AdminCount = _allUsers.Count(u => u.UserRole == UserRole.Admin);
        }

        public void CloseDetailView()
        {
            CloseOverlay();
        }
    }
}
