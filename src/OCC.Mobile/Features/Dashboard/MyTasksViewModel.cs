using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class MyTasksViewModel : ViewModelBase, IDisposable
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectTaskService _taskService;
        private readonly IProjectService _projectService;
        private readonly ISignalRService _signalRService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);
        private System.Threading.CancellationTokenSource? _cts;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _showOverdueOnly;

        [ObservableProperty]
        private bool _showDueTodayOnly;

        [ObservableProperty]
        private bool _showDueThisWeekOnly;

        [ObservableProperty]
        private bool _showOnHoldOnly;

        [ObservableProperty]
        private Guid? _projectId;

        public ObservableCollection<ProjectTask> Tasks { get; } = new();

        // We use a filtered view of the tasks
        public IEnumerable<ProjectTask> FilteredTasks => Tasks.Where(t => 
        {
            // 1. Check Search & Chips
            if (!string.IsNullOrWhiteSpace(SearchText) && !t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                return false;
            
            if (ShowOverdueOnly && !t.IsOverdue)
                return false;

            if (ShowDueTodayOnly && t.FinishDate.Date != DateTime.Today)
                return false;

            if (ShowDueThisWeekOnly)
            {
                var nextWeek = DateTime.Today.AddDays(7);
                if (t.FinishDate.Date < DateTime.Today || t.FinishDate.Date > nextWeek)
                    return false;
            }

            if (ShowOnHoldOnly && !t.IsOnHold)
                return false;

            // 2. Check Hierarchy (Collapse/Expand)
            if (t.ParentId.HasValue)
            {
                var parent = Tasks.FirstOrDefault(p => p.Id == t.ParentId);
                // If parent is found and not expanded, hide this child
                if (parent != null && !parent.IsExpanded)
                    return false;
                
                // Recursively check ancestors if needed, but for now 1-2 levels is enough
                // Let's do a simple recursive check
                while (parent != null && parent.ParentId.HasValue)
                {
                    parent = Tasks.FirstOrDefault(p => p.Id == parent.ParentId);
                    if (parent != null && !parent.IsExpanded)
                        return false;
                }
            }

            return true;
        });

        [RelayCommand]
        public void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            task.IsExpanded = !task.IsExpanded;
            OnPropertyChanged(nameof(FilteredTasks));
        }

        [RelayCommand]
        public void ExpandAll()
        {
            foreach (var task in Tasks)
            {
                task.IsExpanded = true;
            }
            OnPropertyChanged(nameof(FilteredTasks));
        }

        [RelayCommand]
        public void CollapseAll()
        {
            foreach (var task in Tasks)
            {
                task.IsExpanded = false;
            }
            OnPropertyChanged(nameof(FilteredTasks));
        }

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredTasks));
        partial void OnShowOverdueOnlyChanged(bool value) => OnPropertyChanged(nameof(FilteredTasks));
        partial void OnShowDueTodayOnlyChanged(bool value) => OnPropertyChanged(nameof(FilteredTasks));
        partial void OnShowDueThisWeekOnlyChanged(bool value) => OnPropertyChanged(nameof(FilteredTasks));
        partial void OnShowOnHoldOnlyChanged(bool value) => OnPropertyChanged(nameof(FilteredTasks));
        partial void OnProjectIdChanged(Guid? value) => LoadData().FireAndForget();

        public MyTasksViewModel(INavigationService navigationService, IProjectTaskService taskService, IProjectService projectService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _taskService = taskService;
            _projectService = projectService;
            _signalRService = signalRService;
            
            Tasks.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FilteredTasks));
            Title = "My Tasks";

            _signalRService.EntityUpdated += OnEntityUpdated;
            
            if (ProjectId == null)
            {
                LoadData().FireAndForget();
            }
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "ProjectTask" || entityType == "Project")
            {
                LoadData().FireAndForget();
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            _cts?.Cancel();
            _cts?.Dispose();
            base.Dispose();
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<DashboardViewModel>();
        }

        [RelayCommand]
        private void SelectTask(ProjectTask task)
        {
            if (task == null) return;
            _navigationService.NavigateTo<TaskDetailViewModel>(vm => vm.Task = task);
        }

        [RelayCommand]
        public async Task LoadData()
        {
            // Cancel any existing load
            _cts?.Cancel();
            _cts = new System.Threading.CancellationTokenSource();
            var token = _cts.Token;

            await _loadSemaphore.WaitAsync();
            try 
            {
                if (token.IsCancellationRequested) return;

                IsBusy = true;

                // Update Title if ProjectId is set
                if (ProjectId.HasValue)
                {
                    var project = await _projectService.GetProjectAsync(ProjectId.Value);
                    if (project != null)
                    {
                        Title = $"{project.Name} Tasks";
                    }
                    else
                    {
                        Title = "Project Tasks";
                    }
                }
                else
                {
                    Title = "My Tasks";
                }
                
                // Fetch first batch to see if we have anything
                var firstBatch = await _taskService.GetTasksAsync(projectId: ProjectId, assignedToMe: true, skip: 0, take: 50);
                if (token.IsCancellationRequested) return;

                var initialTasks = firstBatch.ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    Tasks.Clear();
                    foreach (var task in initialTasks) Tasks.Add(task);
                });

                if (initialTasks.Count < 50)
                {
                    IsBusy = false;
                    return;
                }

                // Background load the rest
                _ = Task.Run(async () => 
                {
                    int skip = 50;
                    int take = 50;
                    bool hasMore = true;

                    while (hasMore && !token.IsCancellationRequested)
                    {
                        var batch = await _taskService.GetTasksAsync(projectId: ProjectId, assignedToMe: true, skip: skip, take: take);
                        if (token.IsCancellationRequested) break;

                        var batchList = batch.ToList();
                        if (!batchList.Any()) break;

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                        {
                            foreach (var task in batchList) Tasks.Add(task);
                        });

                        skip += take;
                        if (batchList.Count < take) hasMore = false;
                        await Task.Delay(50, token);
                    }
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tasks: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                _loadSemaphore.Release();
            }
        }
    }
}
