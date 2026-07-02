using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.SupportHub.ViewModels
{
    public partial class BugGroup : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BugReport> _items = new();

        [ObservableProperty]
        private bool _isExpanded = true;
    }

    public partial class SupportViewModel : ViewModelBase
    {
        private readonly IBugReportService _bugService;
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SupportViewModel> _logger;
        private List<BugReport> _allBugsCache = new();
        private bool _isSelectingBug;

        [ObservableProperty]
        private ObservableCollection<BugReport> _bugs = new();

        [ObservableProperty]
        private ObservableCollection<BugGroup> _groupedBugs = new();

        [ObservableProperty]
        private BugReport? _selectedBug;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommentCommand))]
        private string _newCommentText = string.Empty;

        [ObservableProperty]
        private bool _isDev;

        [ObservableProperty]
        private bool _isAdmin;

        [ObservableProperty]
        private bool _isReporter;

        public bool CanDeleteSelectedBug => IsDev || IsAdmin || IsReporter;

        public bool CanManageBugs => IsDev || IsAdmin;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statusFilter = "Active";

        [ObservableProperty]
        private string _sortOption = "Date";

        [ObservableProperty]
        private bool _includeArchived;

        [ObservableProperty]
        private bool _showOnlyMyBugs;

        public ObservableCollection<string> StatusFilters { get; } = new() 
        { 
            "Active", "All", "Open", "Fixed", "Resolved", "Closed", "Waiting for Client", "In Progress", "Planning", "Feature Update"
        };
        
        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Priority", "Date"
        };

        public SupportViewModel(
            IBugReportService bugService, 
            IAuthService authService, 
            IPermissionService permissionService,
            IDialogService dialogService,
            ILogger<SupportViewModel> logger)
        {
            _bugService = bugService;
            _authService = authService;
            _permissionService = permissionService;
            _dialogService = dialogService;
            _logger = logger;
            
            Title = "Support Hub";
            
            // Permissions
            IsDev = _permissionService.IsDev;
            IsAdmin = _authService.CurrentUser?.UserRole == UserRole.Admin;
            IncludeArchived = IsDev;

            LoadBugsCommand.Execute(null);
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnStatusFilterChanged(string value) => _ = LoadBugs();
        partial void OnSortOptionChanged(string value) => _ = LoadBugs();
        partial void OnShowOnlyMyBugsChanged(bool value) => _ = LoadBugs();
        partial void OnIncludeArchivedChanged(bool value) => _ = LoadBugs();

        private void ApplyFilters()
        {
            if (_allBugsCache == null) return;

            Func<string, int> getPriority = (s) => s switch {
                "Open" => 0,
                "In Progress" => 1,
                "Planning" => 2,
                "Feature Update" => 3,
                "Fixed" => 4,
                "Waiting for Client" => 5,
                "Resolved" => 6,
                "Closed" => 7,
                _ => 8
            };
            
            var filteredList = _allBugsCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filteredList = filteredList.Where(x => 
                    x.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                    x.ViewName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    x.ReporterName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (ShowOnlyMyBugs)
            {
                var currentUserId = _authService.CurrentUser?.Id;
                filteredList = filteredList.Where(x => x.ReporterId == currentUserId);
            }

            if (StatusFilter == "Active")
            {
                filteredList = filteredList.Where(x => x.Status != "Closed" && x.Status != "Resolved");
            }
            else if (StatusFilter != "All")
            {
                filteredList = filteredList.Where(x => x.Status == StatusFilter);
            }

            if (SortOption == "Date")
            {
                filteredList = filteredList.OrderByDescending(x => x.ReportedDate);
            }
            else
            {
                filteredList = filteredList
                    .OrderBy(x => getPriority(x.Status))
                    .ThenByDescending(x => x.ReportedDate);
            }

            var result = filteredList.ToList();
            Bugs = new ObservableCollection<BugReport>(result);

            var groups = result.GroupBy(x => x.Status)
                               .OrderBy(g => getPriority(g.Key))
                               .Select(g => new BugGroup 
                               { 
                                   Title = g.Key, 
                                   Items = new ObservableCollection<BugReport>(g.ToList()) 
                               });
            
            GroupedBugs = new ObservableCollection<BugGroup>(groups);
        }

        async partial void OnSelectedBugChanged(BugReport? value)
        {
            if (_isSelectingBug) return;

            IsReporter = value?.ReporterId == _authService.CurrentUser?.Id;
            OnPropertyChanged(nameof(CanDeleteSelectedBug));

            if (value != null)
            {
                try
                {
                    _isSelectingBug = true;
                    var fresh = await _bugService.GetBugReportAsync(value.Id);
                    if (fresh != null)
                    {
                        var cacheIndex = _allBugsCache.FindIndex(b => b.Id == fresh.Id);
                        if (cacheIndex >= 0) _allBugsCache[cacheIndex] = fresh;

                        var listIndex = Bugs.IndexOf(value);
                        if (listIndex >= 0) Bugs[listIndex] = fresh;

                        SelectedBug = fresh;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching bug details");
                }
                finally
                {
                    _isSelectingBug = false;
                }
            }
        }

        private async Task LoadBugsInternalAsync()
        {
            var previousId = SelectedBug?.Id;
            var list = await _bugService.GetBugReportsAsync(IncludeArchived);
            
            _allBugsCache = list.ToList();
            ApplyFilters();
            
            if (previousId.HasValue)
            {
                SelectedBug = Bugs.FirstOrDefault(x => x.Id == previousId.Value);
            }
            
            if (SelectedBug == null && Bugs.Any() && !previousId.HasValue)
            {
                SelectedBug = Bugs.First();
            }
        }

        [RelayCommand]
        private async Task LoadBugs()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bugs");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendComment))]
        private async Task SendCommentAsync()
        {
            if (SelectedBug == null || string.IsNullOrWhiteSpace(NewCommentText) || IsBusy) return;

            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, NewCommentText, null);
                NewCommentText = string.Empty;
                await RefreshSelectedBug();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending comment");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSendComment() => !string.IsNullOrWhiteSpace(NewCommentText) && SelectedBug != null && !IsBusy;

        [RelayCommand]
        private async Task MarkAsSolutionAsync(BugComment comment)
        {
            if (comment == null || !IsDev || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.MarkAsSolutionAsync(comment.Id);
                await RefreshSelectedBug();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking solution");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MoveToInProgressAsync()
        {
            if (SelectedBug == null || !CanManageBugs || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, "Moved to In Progress.", "In Progress");
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving status to In Progress");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MoveToPlanningAsync()
        {
            if (SelectedBug == null || !CanManageBugs || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, "Moved to Planning stage.", "Planning");
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving status to Planning");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MoveToFeatureUpdateAsync()
        {
            if (SelectedBug == null || !CanManageBugs || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, "Reclassified as a Feature Update.", "Feature Update");
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving status to Feature Update");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MarkFixedAsync()
        {
            if (SelectedBug == null || !CanManageBugs || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, "Developer marked this issue as Fixed.", "Fixed");
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking bug as fixed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RequestInfoAsync()
        {
            if (SelectedBug == null || !CanManageBugs || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, "Developer requested more information.", "Waiting for Client");
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting info");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CloseBugAsync()
        {
            if (SelectedBug == null || !CanManageBugs || IsBusy) return;
            IsBusy = true;
            try
            {
                await _bugService.AddCommentAsync(SelectedBug.Id, "Developer closed the bug.", "Closed");
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing bug");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void EnlargeScreenshot()
        {
            if (SelectedBug != null && !string.IsNullOrEmpty(SelectedBug.ScreenshotBase64))
            {
                ScreenshotHelper.ShowScreenshot(SelectedBug.ScreenshotBase64, $"Screenshot - {SelectedBug.ViewName}");
            }
        }

        [RelayCommand]
        private async Task DeleteBugAsync()
        {
            if (SelectedBug == null || !CanDeleteSelectedBug || IsBusy) return;
            try
            {
                var title = "Delete Bug Report";
                var message = IsDev || IsAdmin 
                    ? "Are you sure you want to permanently delete this bug report from the database?"
                    : "Are you sure you want to delete your bug report?";
                
                var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
                if (!confirmed) return;

                IsBusy = true;
                var permanent = IsDev || IsAdmin;
                await _bugService.DeleteBugAsync(SelectedBug.Id, permanent);
                
                SelectedBug = null;
                await LoadBugsInternalAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bug");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshSelectedBug()
        {
            if (SelectedBug == null) return;
            var fresh = await _bugService.GetBugReportAsync(SelectedBug.Id);
            if (fresh != null)
            {
                var cacheIndex = _allBugsCache.FindIndex(b => b.Id == fresh.Id);
                if (cacheIndex >= 0)
                {
                    _allBugsCache[cacheIndex] = fresh;
                }
                else
                {
                    _allBugsCache.Add(fresh);
                }
                
                SelectedBug = fresh;
                ApplyFilters();
            }
        }

        [RelayCommand]
        private void SelectBug(BugReport bug)
        {
            SelectedBug = bug;
        }

        [RelayCommand]
        private void CloseHub()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }
}
