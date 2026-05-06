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
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.Admin.Users.ViewModels
{
    public partial class UserListViewModel : ListViewModelBase<User>
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<UserListViewModel> _logger;
        private List<User> _allUsers = new();

        public override string ReportTitle => "System User Directory";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "First Name", PropertyName = "FirstName", Width = 2 },
            new() { Header = "Last Name", PropertyName = "LastName", Width = 2 },
            new() { Header = "Email", PropertyName = "Email", Width = 3 },
            new() { Header = "Role", PropertyName = "UserRole", Width = 1.5 },
            new() { Header = "Approved", PropertyName = "IsApproved", Width = 1 }
        };

        [ObservableProperty] private int _pendingApprovalCount;
        [ObservableProperty] private int _adminCount;

        // Column Visibility
        [ObservableProperty] private bool _isEmailVisible = true;
        [ObservableProperty] private bool _isRoleVisible = true;
        [ObservableProperty] private bool _isStatusVisible = true;
        
        [ObservableProperty] private bool _isColumnPickerOpen;

        private readonly LocalSettingsService _settingsService;

        public UserListViewModel(
            IUserService userService, 
            IAuthService authService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<UserListViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _userService = userService;
            _authService = authService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            Title = "User Management";
            
            LoadLayout();
            _ = LoadDataAsync();
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.UserListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsEmailVisible = layout.Columns.FirstOrDefault(c => c.Header == "Email")?.IsVisible ?? true;
                IsRoleVisible = layout.Columns.FirstOrDefault(c => c.Header == "Role")?.IsVisible ?? true;
                IsStatusVisible = layout.Columns.FirstOrDefault(c => c.Header == "Status")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "Email", IsVisible = IsEmailVisible },
                    new() { Header = "Role", IsVisible = IsRoleVisible },
                    new() { Header = "Status", IsVisible = IsStatusVisible }
                }
            };

            _settingsService.Settings.UserListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsEmailVisibleChanged(bool value) => SaveLayout();
        partial void OnIsRoleVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStatusVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

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
            OpenOverlay(new UserDetailViewModel(this, user, _userService, _dialogService, _logger, _pdfService));
        }

        [RelayCommand]
        public void EditUser(User? user)
        {
            var target = user ?? SelectedItem;
            if (target == null) return;
            OpenOverlay(new UserDetailViewModel(this, target, _userService, _dialogService, _logger, _pdfService));
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
