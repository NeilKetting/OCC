using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.AdminDashboard
{
    public partial class ActiveProjectsViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectService _projectService;
        private readonly ObservableCollection<Project> _allProjects = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<Project> Projects { get; } = new();

        public ActiveProjectsViewModel(INavigationService navigationService, IProjectService projectService)
        {
            _navigationService = navigationService;
            _projectService = projectService;
            Title = "Active Projects";
            LoadData();
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<AdminDashboardViewModel>();
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterProjects();
        }

        private void FilterProjects()
        {
            Projects.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allProjects
                : _allProjects.Where(p => p.Name.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) || 
                                          p.ProjectManager.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase));

            foreach (var project in filtered)
            {
                Projects.Add(project);
            }
        }

        private async void LoadData()
        {
            IsBusy = true;
            var projects = await _projectService.GetProjectsAsync(assignedToMe: true);
            _allProjects.Clear();
            foreach (var project in projects.Where(p => p.Status != "Completed" && p.Status != "Done" && p.Status != "Cancelled"))
            {
                // Calculate task counts for the card
                project.ProjectManager = project.Tasks.Count(t => !t.IsComplete && t.FinishDate.Date == System.DateTime.Today).ToString();
                project.Description = project.Tasks.Count(t => t.IsOverdue).ToString();
                
                _allProjects.Add(project);
            }
            FilterProjects();
            IsBusy = false;
        }

        [RelayCommand]
        private void NavigateToProjectTasks(Project project)
        {
            if (project != null)
            {
                _navigationService.NavigateTo<Dashboard.MyTasksViewModel>(vm => vm.ProjectId = project.Id);
            }
        }

        [RelayCommand]
        private void NavigateToDueToday(Project project)
        {
            if (project != null)
            {
                _navigationService.NavigateTo<Dashboard.MyTasksViewModel>(vm => 
                {
                    vm.ProjectId = project.Id;
                    vm.ShowDueTodayOnly = true;
                });
            }
        }

        [RelayCommand]
        private void NavigateToOverdue(Project project)
        {
            if (project != null)
            {
                _navigationService.NavigateTo<Dashboard.MyTasksViewModel>(vm => 
                {
                    vm.ProjectId = project.Id;
                    vm.ShowOverdueOnly = true;
                });
            }
        }

        [RelayCommand]
        private void NavigateToInventory(Project project)
        {
            if (project != null)
            {
                _navigationService.NavigateTo<Dashboard.InventoryViewModel>(vm => 
                {
                    vm.ProjectId = project.Id;
                    vm.LoadDataCommand.Execute(null);
                });
            }
        }

        [RelayCommand]
        private void NavigateToTeam(Project project)
        {
            if (project != null)
            {
                _navigationService.NavigateTo<Dashboard.TeamViewModel>(vm => 
                {
                    vm.ProjectId = project.Id;
                    vm.LoadDataCommand.Execute(null);
                });
            }
        }

        [RelayCommand]
        private void NavigateToProjectHseq(Project project)
        {
            if (project != null)
            {
                _navigationService.NavigateTo<HSEQ.HseqListViewModel>();
            }
        }
    }
}
