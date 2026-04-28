using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly IProjectTaskService _taskService;
        private readonly ISignalRService _signalRService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

        [ObservableProperty]
        private int _activeSitesCount;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.Project> _activeProjects = new();

        [ObservableProperty]
        private ObservableCollection<OCC.Shared.DTOs.DashboardUpdateDto> _recentActivity = new();

        [ObservableProperty]
        private OCC.Shared.Models.Project? _selectedProject;

        public DashboardViewModel(INavigationService navigationService, IProjectService projectService, IProjectTaskService taskService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _taskService = taskService;
            _signalRService = signalRService;
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            Title = "Field Operations";
            LoadData();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask" || entityType == "DashboardUpdate")
            {
                // Refresh project list and activity in real-time
                LoadData().FireAndForget();
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }

        public async Task LoadData()
        {
            if (!await _loadSemaphore.WaitAsync(0)) return;
            try
            {
                // 1. Fetch Projects
                var projects = await _projectService.GetProjectsAsync(assignedToMe: true);
                var projectList = projects.GroupBy(p => p.Id).Select(g => g.First()).ToList(); 
                
                var newCollection = new System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.Project>();
                foreach (var p in projectList)
                {
                    // Calculate task counts for the card
                    p.ProjectManager = p.Tasks.Count(t => !t.IsComplete && t.FinishDate.Date == DateTime.Today).ToString();
                    p.Description = p.Tasks.Count(t => t.IsOverdue).ToString();
                    
                    newCollection.Add(p);
                }

                // 2. Fetch Recent Activity
                var updates = await _taskService.GetRecentUpdatesAsync();
                var activityList = updates.Take(10).ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    ActiveProjects = newCollection;
                    ActiveSitesCount = projectList.Count;
                    
                    RecentActivity.Clear();
                    foreach (var update in activityList)
                    {
                        RecentActivity.Add(update);
                    }

                    // Default to first project if none selected
                    if (SelectedProject == null || !projectList.Any(p => p.Id == SelectedProject.Id))
                    {
                        SelectedProject = projectList.FirstOrDefault();
                    }
                });
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        [RelayCommand]
        private void SelectProject(OCC.Shared.Models.Project project)
        {
            SelectedProject = project;
        }

        [RelayCommand]
        private void NavigateToProjectTasks(OCC.Shared.Models.Project project)
        {
            _navigationService.NavigateTo<MyTasksViewModel>(vm => vm.ProjectId = project.Id);
        }

        [RelayCommand]
        private void NavigateToDueToday(OCC.Shared.Models.Project project)
        {
            _navigationService.NavigateTo<MyTasksViewModel>(vm => 
            {
                vm.ProjectId = project.Id;
                vm.ShowDueTodayOnly = true;
            });
        }

        [RelayCommand]
        private void NavigateToOverdue(OCC.Shared.Models.Project project)
        {
            _navigationService.NavigateTo<MyTasksViewModel>(vm => 
            {
                vm.ProjectId = project.Id;
                vm.ShowOverdueOnly = true;
            });
        }

        [RelayCommand]
        private void NavigateToInventory(OCC.Shared.Models.Project project)
        {
            _navigationService.NavigateTo<InventoryViewModel>(vm => 
            {
                vm.ProjectId = project.Id;
                vm.LoadDataCommand.Execute(null);
            });
        }

        [RelayCommand]
        private void NavigateToTeam(OCC.Shared.Models.Project project)
        {
            _navigationService.NavigateTo<TeamViewModel>(vm => 
            {
                vm.ProjectId = project.Id;
                vm.LoadDataCommand.Execute(null);
            });
        }

        [RelayCommand]
        private void NavigateToProjectHseq(OCC.Shared.Models.Project project)
        {
            _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
        }

        [RelayCommand]
        private void NavigateToMyTasks()
        {
            _navigationService.NavigateTo<MyTasksViewModel>();
        }

        [RelayCommand]
        private void NavigateToHseq()
        {
            _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
        }
    }
}
