using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;

namespace OCC.Client.Features.BugHub.ViewModels
{
    public class BugGroup : ObservableObject
    {
        public string Title { get; set; } = string.Empty;
        public ObservableCollection<BugReport> Items { get; set; } = new();
        public bool IsExpanded { get; set; } = true;
    }

    public partial class BugListViewModel : ViewModelBase, IRecipient<EntityUpdatedMessage>
    {
        #region Private Members

        private readonly IBugReportService _bugService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IPermissionService _permissionService;
        private readonly Microsoft.Extensions.Logging.ILogger<BugListViewModel> _logger;
        private List<BugReport> _allBugsCache = new();
        private bool _isSelectingBug;

        #endregion

        #region Observables

        [ObservableProperty]
        private ObservableCollection<BugReport> _bugs = new();

        [ObservableProperty]
        private ObservableCollection<BugGroup> _groupedBugs = new();

        [ObservableProperty]
        private BugReport? _selectedBug;

        [ObservableProperty]
        private string _newCommentText = string.Empty;

        [ObservableProperty]
        private bool _isDev;

        [ObservableProperty]
        private bool _isReporter;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap? _selectedBugScreenshot;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statusFilter = "Active"; // Active, All, Open, Fixed, Resolved, Closed, Waiting for Client
        
        [ObservableProperty]
        private string _sortOption = "Date"; // Priority, Date

        [ObservableProperty]
        private bool _isImageZoomed;

        [ObservableProperty]
        private bool _includeArchived;

        public ObservableCollection<string> StatusFilters { get; } = new() 
        { 
            "Active", "All", "Open", "Fixed", "Resolved", "Closed", "Waiting for Client", "In Progress", "Planning", "Feature Update"
        };
        
        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Priority", "Date"
        };

        #endregion

        #region Constructor

        public BugListViewModel(IBugReportService bugService, IAuthService authService, IDialogService dialogService, IPermissionService permissionService, Microsoft.Extensions.Logging.ILogger<BugListViewModel> logger)
        {
            _bugService = bugService;
            _authService = authService;
            _dialogService = dialogService;
            _permissionService = permissionService;
            _logger = logger;
            
            // Permissions: "Dev" is specifically Neil for commenting purposes now
            var email = _authService.CurrentUser?.Email?.ToLowerInvariant();
            IsDev = email == "neil@mdk.co.za";

            WeakReferenceMessenger.Default.Register<EntityUpdatedMessage>(this);

            LoadBugsCommand.Execute(null);
        }

        #endregion

        #region Methods

        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "BugReport")
            {
                Dispatcher.UIThread.Post(async () => 
                {
                    if (message.Value.Action == "Delete")
                    {
                        if (SelectedBug?.Id == message.Value.Id) SelectedBug = null;
                        await LoadBugs();
                    }
                    else if (message.Value.Action == "Update")
                    {
                        if (SelectedBug?.Id == message.Value.Id)
                        {
                            await RefreshSelectedBug();
                        }
                        else 
                        {
                            await LoadBugs();
                        }
                    }
                });
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnStatusFilterChanged(string value) => ApplyFilters();
        partial void OnSortOptionChanged(string value) => ApplyFilters();
        partial void OnIncludeArchivedChanged(bool value) => LoadBugsCommand.Execute(null);

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

            if (StatusFilter == "Active")
            {
                filteredList = filteredList.Where(x => x.Status != "Closed" && x.Status != "Resolved");
            }
            else if (StatusFilter != "All")
            {
                filteredList = filteredList.Where(x => x.Status == StatusFilter);
            }

            // sort logic
            if (SortOption == "Date")
            {
                filteredList = filteredList.OrderByDescending(x => x.ReportedDate);
            }
            else
            {
                // Priority Sort
                filteredList = filteredList
                    .OrderBy(x => getPriority(x.Status))
                    .ThenByDescending(x => x.ReportedDate);
            }

            var result = filteredList.ToList();
            Bugs = new ObservableCollection<BugReport>(result);

            // Grouping logic
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
            SelectedBugScreenshot = null;

            if (value != null)
            {
                try
                {
                    _isSelectingBug = true;
                    
                    // Fetch fresh details with comments and screenshot
                    var fresh = await _bugService.GetBugReportAsync(value.Id);
                    if (fresh != null)
                    {
                        // Update cache
                        var cacheIndex = _allBugsCache.FindIndex(b => b.Id == fresh.Id);
                        if (cacheIndex >= 0) _allBugsCache[cacheIndex] = fresh;

                        // Update current list to keep UI in sync
                        var listIndex = Bugs.IndexOf(value);
                        if (listIndex >= 0)
                        {
                            Bugs[listIndex] = fresh;
                        }

                        // Re-assign to force notification of the change to the full object
                        SelectedBug = fresh;

                        if (!string.IsNullOrEmpty(fresh.ScreenshotBase64))
                        {
                            try
                            {
                                var bytes = Convert.FromBase64String(fresh.ScreenshotBase64);
                                using (var ms = new System.IO.MemoryStream(bytes))
                                {
                                    SelectedBugScreenshot = new Avalonia.Media.Imaging.Bitmap(ms);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error decoding screenshot: {ex.Message}");
                                SelectedBugScreenshot = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching bug details: {ex.Message}");
                }
                finally
                {
                    _isSelectingBug = false;
                }
            }
        }

        [RelayCommand]
        private void SelectBug(BugReport bug)
        {
            SelectedBug = bug;
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
                await _dialogService.ShowAlertAsync("Error", $"Failed to load bug reports: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshSelectedBug()
        {
            if (SelectedBug == null) return;
            try 
            {
                var fresh = await _bugService.GetBugReportAsync(SelectedBug.Id);
                if (fresh != null)
                {
                    // Replace item in cache
                    var cacheIndex = _allBugsCache.FindIndex(b => b.Id == fresh.Id);
                    if (cacheIndex >= 0) _allBugsCache[cacheIndex] = fresh;

                    // Update UI object properties
                    // To ensure UI updates, we replace the item if it's in the current filtered Bugs list
                    var bugInList = Bugs.FirstOrDefault(b => b.Id == fresh.Id);
                    if (bugInList != null)
                    {
                        var listIndex = Bugs.IndexOf(bugInList);
                        Bugs[listIndex] = fresh;
                    }

                    // Refresh grouped view if status changed
                    if (SelectedBug.Status != fresh.Status)
                    {
                        ApplyFilters();
                    }

                    SelectedBug = fresh;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SendComment()
        {
            if (SelectedBug == null || string.IsNullOrWhiteSpace(NewCommentText)) return;

            try
            {
                if (!IsDev && !IsReporter)
                {
                    await _dialogService.ShowAlertAsync("Access Denied", "Only the Developer (Neil) or the Reporter can comment on this bug.");
                    return;
                }

                await _bugService.AddCommentAsync(SelectedBug.Id, NewCommentText, null);
                NewCommentText = string.Empty;
                // SignalR will trigger refresh
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to send comment: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task MoveToInProgress()
        {
            if (SelectedBug == null || !IsDev) return;
            await _bugService.AddCommentAsync(SelectedBug.Id, "Moved to In Progress.", "In Progress");
        }

        [RelayCommand]
        private async Task MoveToPlanning()
        {
            if (SelectedBug == null || !IsDev) return;
            await _bugService.AddCommentAsync(SelectedBug.Id, "Moved to Planning stage.", "Planning");
        }

        [RelayCommand]
        private async Task MoveToFeatureUpdate()
        {
            if (SelectedBug == null || !IsDev) return;
            await _bugService.AddCommentAsync(SelectedBug.Id, "Reclassified as a Feature Update.", "Feature Update");
        }

        [RelayCommand]
        private async Task RequestInfo()
        {
            if (SelectedBug == null || !IsDev) return;
            var text = "Developer requested more information.";
            await _bugService.AddCommentAsync(SelectedBug.Id, text, "Waiting for Client");
        }

        [RelayCommand]
        private async Task MarkFixed()
        {
            if (SelectedBug == null || !IsDev) return;
            var text = "Developer marked this issue as Fixed. Please verify and mark as Resolved.";
            await _bugService.AddCommentAsync(SelectedBug.Id, text, "Fixed");
        }

        [RelayCommand]
        private async Task CloseBug()
        {
            if (SelectedBug == null || !IsDev) return;
            var text = "Developer closed the bug.";
            await _bugService.AddCommentAsync(SelectedBug.Id, text, "Closed");
        }

        [RelayCommand]
        private async Task DeleteBug()
        {
             if (SelectedBug == null) return;
             
             // Extra safety on deletion
             var email = _authService.CurrentUser?.Email?.ToLowerInvariant();
             if (email != "neil@mdk.co.za") 
             {
                 await _dialogService.ShowAlertAsync("Access Denied", "Only Neil can delete records.");
                 return;
             }

             var result = await _dialogService.ShowConfirmationAsync("Delete Bug Report", "Are you sure you want to permanently delete this bug report?");
             if (!result) return;
             
             try
             {
                 await _bugService.DeleteBugAsync(SelectedBug.Id);
                 SelectedBug = null;
                 // SignalR will handle list refresh
             }
             catch(Exception ex)
             {
                 await _dialogService.ShowAlertAsync("Error", $"Delete failed: {ex.Message}");
             }
        }

        [RelayCommand]
        private async Task DeleteComment(BugComment comment)
        {
            if (comment == null || !IsDev) return;

            var result = await _dialogService.ShowConfirmationAsync("Delete Comment", "Are you sure you want to delete this comment?");
            if (!result) return;

            try
            {
                await _bugService.DeleteCommentAsync(comment.Id);
                // SignalR will trigger refresh via EntityUpdate on the BugReport
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to delete comment: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task MarkResolved()
        {
            if (SelectedBug == null) return;
            var text = $"{_authService.CurrentUser?.FirstName ?? "Reporter"} marked this issue as Resolved.";
            await _bugService.AddCommentAsync(SelectedBug.Id, text, "Resolved");
            await RefreshSelectedBug();
        }

        [RelayCommand]
        private async Task MarkNotResolved()
        {
            if (SelectedBug == null) return;
            var text = $"{_authService.CurrentUser?.FirstName ?? "Reporter"} marked this issue as Still Broken.";
            await _bugService.AddCommentAsync(SelectedBug.Id, text, "Open");
            await RefreshSelectedBug();
        }

        [RelayCommand]
        private void ToggleImageZoom()
        {
            IsImageZoomed = !IsImageZoomed;
        }

        [RelayCommand]
        private async Task OpenLogFolder()
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OCC", "logs");

                if (!System.IO.Directory.Exists(logPath))
                {
                   System.IO.Directory.CreateDirectory(logPath);
                }

                // Use Process.Start with "explorer.exe" and the path arguments
                // This is the most reliable way on Windows to open a folder
                System.Diagnostics.Process.Start("explorer.exe", logPath);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to open log folder: {ex.Message}");
            }
        }

        #endregion
    }
}
