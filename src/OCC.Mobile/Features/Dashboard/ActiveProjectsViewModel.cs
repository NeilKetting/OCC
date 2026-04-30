using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class ActiveProjectsViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly ISignalRService _signalRService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

        [ObservableProperty]
        private ObservableCollection<OCC.Shared.Models.Project> _activeProjects = new();

        public ActiveProjectsViewModel(INavigationService navigationService, IProjectService projectService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _signalRService = signalRService;
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            Title = "My Projects";
            LoadData().FireAndForget();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask")
            {
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
                var projects = await _projectService.GetProjectsAsync(assignedToMe: true);
                var projectList = projects.GroupBy(p => p.Id).Select(g => g.First()).ToList(); 
                
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    ActiveProjects.Clear();
                    foreach (var p in projectList)
                    {
                        // Pre-calculate counts for badges if needed
                        p.ProjectManager = p.Tasks.Count(t => !t.IsComplete && t.FinishDate.Date == DateTime.Today).ToString();
                        p.Description = p.Tasks.Count(t => t.IsOverdue).ToString();
                        ActiveProjects.Add(p);
                    }
                });
            }
            finally
            {
                _loadSemaphore.Release();
            }
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
    }
}
