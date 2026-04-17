using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace OCC.Client.Features.BugHub.ViewModels
{
    public partial class SupportCenterViewModel : ViewModelBase, IRecipient<EntityUpdatedMessage>
    {
        private readonly IBugReportService _bugService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;

        [ObservableProperty] private ObservableCollection<BugReport> _myIssues = new();
        [ObservableProperty] private ObservableCollection<BugReport> _solvedIssues = new();
        [ObservableProperty] private BugReport? _selectedIssue;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _newCommentText = string.Empty;
        [ObservableProperty] private bool _isSearching;
        [ObservableProperty] private bool _isDetailViewVisible;

        public SupportCenterViewModel(IBugReportService bugService, IAuthService authService, IDialogService dialogService)
        {
            _bugService = bugService;
            _authService = authService;
            _dialogService = dialogService;

            WeakReferenceMessenger.Default.Register<EntityUpdatedMessage>(this);
            LoadDataCommand.Execute(null);
        }

        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "BugReport")
            {
                if (SelectedIssue?.Id == message.Value.Id)
                {
                    RefreshSelectedIssue().ConfigureAwait(false);
                }
                else 
                {
                    LoadDataCommand.Execute(null);
                }
            }
        }

        [RelayCommand]
        private async Task LoadData()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var allBugs = await _bugService.GetBugReportsAsync(false);
                var currentUserEmail = _authService.CurrentUser?.Email?.ToLowerInvariant();
                
                // My Issues: Reported by me or explicitly assigned (if that existed)
                var list = await _bugService.GetBugReportsAsync(false);
                MyIssues = new ObservableCollection<BugReport>(list.Where(b => b.ReporterId == _authService.CurrentUser?.Id));
                
                // Fetch top recent solutions for the Knowledge Base sidebar
                var solutions = await _bugService.SearchSolutionsAsync(""); // Empty search for recent? No, maybe just fetch some.
                // Or better: update SearchSolutionsAsync to handle empty query as "Recent" or use a different call.
                // For now, let's use the list we have for 'Fixed' ones, and maybe a small search.
                if (list.Any(b => b.Status == "Fixed"))
                {
                    SolvedIssues = new ObservableCollection<BugReport>(list.Where(b => b.Status == "Fixed").Take(5));
                }
                else 
                {
                    // Try a generic search for "help" or similar to get something in the sidebar
                    var recentSolutions = await _bugService.SearchSolutionsAsync("a"); // hacky "get some" 
                    SolvedIssues = new ObservableCollection<BugReport>(recentSolutions.Take(5));
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Refresh Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectIssue(BugReport? bug)
        {
            SelectedIssue = bug;
            IsDetailViewVisible = bug != null;
            if (bug != null) _ = RefreshSelectedIssue();
        }

        private async Task RefreshSelectedIssue()
        {
            if (SelectedIssue == null) return;
            var fresh = await _bugService.GetBugReportAsync(SelectedIssue.Id);
            if (fresh != null) SelectedIssue = fresh;
        }

        [RelayCommand]
        private async Task SendFeedback()
        {
            if (SelectedIssue == null || string.IsNullOrWhiteSpace(NewCommentText)) return;
            try
            {
                await _bugService.AddCommentAsync(SelectedIssue.Id, NewCommentText, null);
                NewCommentText = string.Empty;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task MarkAsFixed()
        {
            if (SelectedIssue == null) return;
            var result = await _dialogService.ShowConfirmationAsync("Confirm Fix", "By marking this as fixed, you confirm that the issue is resolved on your end. The report will be closed. Proceed?");
            if (!result) return;

            try
            {
                await _bugService.AddCommentAsync(SelectedIssue.Id, "User confirmed the fix. Closing report.", "Resolved");
                IsDetailViewVisible = false;
                SelectedIssue = null;
                await LoadData();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task SearchSolutions()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                IsSearching = false;
                await LoadData();
                return;
            }

            IsSearching = true;
            IsBusy = true;
            try
            {
                var results = await _bugService.SearchSolutionsAsync(SearchText);
                MyIssues = new ObservableCollection<BugReport>(results);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            // Debounce or just trigger command after slight delay or on Enter
            // For now, we'll just expose the command for the UI to bind to (e.g., on KeyUp or Search icon)
            // But let's trigger it automatically for that "exciting" feel
            if (value?.Length > 2)
            {
                _ = SearchSolutions();
            }
            else if (string.IsNullOrEmpty(value))
            {
                 IsSearching = false;
                 _ = LoadData();
            }
        }

        [RelayCommand]
        private void CloseDetail()
        {
            IsDetailViewVisible = false;
            SelectedIssue = null;
        }
    }
}
