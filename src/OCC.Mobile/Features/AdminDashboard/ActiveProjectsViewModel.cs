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
            var projects = await _projectService.GetProjectsAsync();
            _allProjects.Clear();
            foreach (var project in projects.Where(p => p.Status == "Active"))
            {
                _allProjects.Add(project);
            }
            FilterProjects();
            IsBusy = false;
        }
    }
}
