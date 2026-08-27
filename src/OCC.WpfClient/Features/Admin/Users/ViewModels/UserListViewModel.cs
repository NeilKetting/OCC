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
        private readonly IEmployeeService _employeeService;
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
            IEmployeeService employeeService,
            LocalSettingsService settingsService,
            ILogger<UserListViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _userService = userService;
            _authService = authService;
            _dialogService = dialogService;
            _employeeService = employeeService;
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
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
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
                
                var users = (await _userService.GetUsersAsync()).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
                
                if (users.Count > 100)
                {
                    // Step 1: Fast render top 100
                    _allUsers = users.Take(100).ToList();
                    FilterItems();
                    IsBusy = false; // Unblock UI

                    // Step 2: Background hydration
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            _allUsers = users;
                            FilterItems();
                        });
                    });
                }
                else
                {
                    _allUsers = users;
                    FilterItems();
                }
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
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            // Check if any of the target users are linked to employee records
            bool anyLinked = false;
            try
            {
                var employees = await _employeeService.GetEmployeesAsync();
                var linkedUserIds = employees
                    .Where(e => e.LinkedUserId.HasValue)
                    .Select(e => e.LinkedUserId!.Value)
                    .ToHashSet();

                anyLinked = targets.Any(t => linkedUserIds.Contains(t.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load employee records for dependency validation.");
            }

            string title = targets.Count > 1 ? "Delete Multiple Users" : "Delete User";
            string message;
            if (anyLinked)
            {
                message = targets.Count > 1
                    ? "One or more of the selected users are linked to active employee profiles.\n\nDeleting these user accounts will remove their access to the application and unlink them from their employee records. This action cannot be undone.\n\nAre you sure you want to proceed?"
                    : $"The user '{targets[0].FirstName} {targets[0].LastName}' is linked to an active employee profile.\n\nDeleting this user account will remove their access to the application and unlink them from their employee record. This action cannot be undone.\n\nAre you sure you want to proceed?";
            }
            else
            {
                message = targets.Count > 1 
                    ? $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?"
                    : $"Are you sure you want to delete user '{targets[0].FirstName} {targets[0].LastName}'? This action cannot be undone.";
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = targets.Count > 1 ? "Deleting users..." : "Deleting user...";
                var failedTargets = new List<User>();
                foreach (var t in targets)
                {
                    var success = await _userService.DeleteUserAsync(t.Id);
                    if (!success)
                    {
                        failedTargets.Add(t);
                    }
                }
                await LoadDataAsync();

                if (failedTargets.Any())
                {
                    var failedNames = string.Join(", ", failedTargets.Select(u => $"'{u.FirstName} {u.LastName}'"));
                    await _dialogService.ShowAlertAsync("Delete Failed", $"Failed to delete the following user(s): {failedNames}.\n\nPlease check system restrictions (e.g. you cannot delete the developer account unless a duplicate exists, and you cannot delete your own active account).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk delete failed");
                await _dialogService.ShowAlertAsync("Error", $"An unexpected error occurred during delete: {ex.Message}");
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
                filtered = filtered.Where(u => SearchUtils.MatchesQuery(SearchQuery, u.FirstName, u.LastName, u.Email, u.Branch?.ToString()));
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
