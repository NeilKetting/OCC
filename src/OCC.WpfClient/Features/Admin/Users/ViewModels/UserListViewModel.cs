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
            new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 },
            new() { Header = "Branch", PropertyName = "Location", Width = 1.5 },
            new() { Header = "Company", PropertyName = "CompanyName", Width = 2 },
            new() { Header = "Role", PropertyName = "UserRole", Width = 1.5 },
            new() { Header = "Approved", PropertyName = "IsApproved", Width = 1 }
        };

        [ObservableProperty] private int _pendingApprovalCount;
        [ObservableProperty] private int _adminCount;

        // Column Visibility
        [ObservableProperty] private bool _isEmailVisible = true;
        [ObservableProperty] private bool _isRoleVisible = true;
        [ObservableProperty] private bool _isStatusVisible = true;
        [ObservableProperty] private bool _isPhoneVisible = false;
        [ObservableProperty] private bool _isLocationVisible = true;
        [ObservableProperty] private bool _isCompanyVisible = false;
        [ObservableProperty] private bool _isCreatedDateVisible = false;
        
        

        private readonly LocalSettingsService _settingsService;

        // Standard commands for centralized UI
        public override IRelayCommand<object>? OpenCommand => OpenUserCommand;
        public override IRelayCommand<object>? EditCommand => EditUserCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedUsersCommand;

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
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? false;
                IsLocationVisible = layout.Columns.FirstOrDefault(c => c.Header == "Branch")?.IsVisible ?? true;
                IsCompanyVisible = layout.Columns.FirstOrDefault(c => c.Header == "Company")?.IsVisible ?? false;
                IsCreatedDateVisible = layout.Columns.FirstOrDefault(c => c.Header == "Created")?.IsVisible ?? false;
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
                    new() { Header = "Status", IsVisible = IsStatusVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible },
                    new() { Header = "Branch", IsVisible = IsLocationVisible },
                    new() { Header = "Company", IsVisible = IsCompanyVisible },
                    new() { Header = "Created", IsVisible = IsCreatedDateVisible }
                }
            };

            _settingsService.Settings.UserListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsEmailVisibleChanged(bool value) => SaveLayout();
        partial void OnIsRoleVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStatusVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPhoneVisibleChanged(bool value) => SaveLayout();
        partial void OnIsLocationVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCompanyVisibleChanged(bool value) => SaveLayout();
        partial void OnIsCreatedDateVisibleChanged(bool value) => SaveLayout();

        

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
            var detailVm = new UserDetailViewModel(user, _userService, _dialogService, _logger, _pdfService);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private void OpenUser(object? parameter)
        {
            _ = EditUser(parameter);
        }

        [RelayCommand]
        private async Task EditUser(object? parameter)
        {
            var target = parameter as User ?? SelectedItem;
            if (target == null) return;
            var detailVm = new UserDetailViewModel(target, _userService, _dialogService, _logger, _pdfService);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private async Task DeleteSelectedUsers(object? parameter)
        {
            List<User> targets = new();
            if (parameter is System.Collections.IList list)
            {
                targets = list.Cast<User>().ToList();
            }
            else if (parameter is User user)
            {
                targets.Add(user);
            }
            else if (SelectedItem != null)
            {
                targets.Add(SelectedItem);
            }

            if (!targets.Any()) return;

            string message = targets.Count > 1 
                ? $"Are you sure you want to delete {targets.Count} selected users? This action cannot be undone."
                : $"Are you sure you want to delete user '{targets[0].FirstName} {targets[0].LastName}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync("Delete User", message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting...";
                foreach (var t in targets)
                {
                    await _userService.DeleteUserAsync(t.Id);
                }
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk delete failed");
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

        
    }
}
