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
        private readonly ISignalRService _signalRService;
        private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

        [ObservableProperty]
        private int _activeSitesCount;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.Project> _activeProjects = new();

        [ObservableProperty]
        private OCC.Shared.Models.Project? _selectedProject;

        public DashboardViewModel(INavigationService navigationService, IProjectService projectService, ISignalRService signalRService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            _signalRService = signalRService;
            
            _signalRService.EntityUpdated += OnEntityUpdated;
            
            Title = "Field Operations";
            LoadData();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask")
            {
                // Refresh project list in real-time
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
                var projectList = projects.GroupBy(p => p.Id).Select(g => g.First()).ToList(); // Paranoia: Ensure uniqueness in VM
                
                var newCollection = new System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.Project>();
                foreach (var p in projectList)
                {
                    // Calculate task counts for the card
                    p.ProjectManager = p.Tasks.Count(t => !t.IsComplete && t.FinishDate.Date == DateTime.Today).ToString();
                    p.Description = p.Tasks.Count(t => t.IsOverdue).ToString();
                    
                    newCollection.Add(p);
                }

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    ActiveProjects = newCollection;
                    ActiveSitesCount = projectList.Count;

                    // Default to first project if none selected or if selected project is no longer in list
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
        private void NavigateToProjectHseq(OCC.Shared.Models.Project project)
        {
            // For now HSEQ doesn't support project filtering in its VM, 
            // but we can pass it if we update HseqListViewModel later.
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
