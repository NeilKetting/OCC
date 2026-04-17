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
        private bool _isReporter;

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
            ILogger<SupportViewModel> logger)
        {
            _bugService = bugService;
            _authService = authService;
            _permissionService = permissionService;
            _logger = logger;
            
            Title = "Support Hub";
            
            // Permissions: "Dev" check based on email
            var email = _authService.CurrentUser?.Email?.ToLowerInvariant();
            IsDev = email == "neil@mdk.co.za";

            LoadBugsCommand.Execute(null);
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnStatusFilterChanged(string value) => ApplyFilters();
        partial void OnSortOptionChanged(string value) => ApplyFilters();
        partial void OnShowOnlyMyBugsChanged(bool value) => ApplyFilters();
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

        [RelayCommand]
        private async Task LoadBugs()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
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
            if (SelectedBug == null || string.IsNullOrWhiteSpace(NewCommentText)) return;

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
        }

        private bool CanSendComment() => !string.IsNullOrWhiteSpace(NewCommentText) && SelectedBug != null;

        [RelayCommand]
        private async Task MarkAsSolutionAsync(BugComment comment)
        {
            if (comment == null || !IsDev) return;
            try
            {
                await _bugService.MarkAsSolutionAsync(comment.Id);
                await RefreshSelectedBug();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking solution");
            }
        }

        [RelayCommand]
        private async Task MarkFixedAsync()
        {
            if (SelectedBug == null || !IsDev) return;
            await _bugService.AddCommentAsync(SelectedBug.Id, "Developer marked this issue as Fixed.", "Fixed");
            await RefreshSelectedBug();
        }

        [RelayCommand]
        private async Task RequestInfoAsync()
        {
            if (SelectedBug == null || !IsDev) return;
            await _bugService.AddCommentAsync(SelectedBug.Id, "Developer requested more information.", "Waiting for Client");
            await RefreshSelectedBug();
        }

        [RelayCommand]
        private async Task CloseBugAsync()
        {
            if (SelectedBug == null || !IsDev) return;
            await _bugService.AddCommentAsync(SelectedBug.Id, "Developer closed the bug.", "Closed");
            await RefreshSelectedBug();
        }

        [RelayCommand]
        private async Task DeleteBugAsync()
        {
            if (SelectedBug == null || !IsDev) return;
            try
            {
                await _bugService.DeleteBugAsync(SelectedBug.Id);
                SelectedBug = null;
                await LoadBugs();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bug");
            }
        }

        private async Task RefreshSelectedBug()
        {
            if (SelectedBug == null) return;
            var fresh = await _bugService.GetBugReportAsync(SelectedBug.Id);
            if (fresh != null)
            {
                SelectedBug = fresh;
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
