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
    public partial class UserListViewModel : ListViewModelBase<User>
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<UserListViewModel> _logger;
        private List<User> _allUsers = new();

        [ObservableProperty] private int _pendingApprovalCount;
        [ObservableProperty] private int _adminCount;

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
            
            _ = LoadDataAsync();
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading users...";
                
                var users = await _userService.GetUsersAsync();
                _allUsers = users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
                
                FilterItems();
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
            var target = user ?? SelectedItem;
            if (target == null) return;
            OpenOverlay(new UserDetailViewModel(this, target, _userService, _dialogService, _logger));
        }

        [RelayCommand]
        private async Task DeleteUser(User? user)
        {
            var target = user ?? SelectedItem;
            if (target == null) return;
            
            var confirmed = await _dialogService.ShowConfirmationAsync("Delete User", 
                $"Are you sure you want to delete user '{target.FirstName} {target.LastName}'?");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting user...";
                var success = await _userService.DeleteUserAsync(target.Id);
                if (success)
                {
                    await LoadDataAsync();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected override void FilterItems()
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
            Items = new ObservableCollection<User>(result);

            // Update Stats
            TotalCount = result.Count;
            PendingApprovalCount = result.Count(u => !u.IsApproved);
            AdminCount = result.Count(u => u.UserRole == UserRole.Admin);
        }

        public void CloseDetailView() => CloseOverlay();
    }
}
