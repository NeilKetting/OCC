using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.AdminDashboard
{
    public partial class OverdueTasksViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IProjectTaskService _taskService;

        public ObservableCollection<ProjectTaskGroup> GroupedTasks { get; } = new();

        public OverdueTasksViewModel(INavigationService navigationService, IProjectTaskService taskService)
        {
            _navigationService = navigationService;
            _taskService = taskService;
            Title = "Overdue Tasks";
            LoadData();
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<AdminDashboardViewModel>();
        }

        private async void LoadData()
        {
            IsBusy = true;
            var tasks = await _taskService.GetTasksAsync();
            var overdue = tasks.Where(t => t.IsOverdue).ToList();
            
            GroupedTasks.Clear();
            var groups = overdue.GroupBy(t => t.Project?.Name ?? "Unknown Project");
            foreach (var group in groups)
            {
                GroupedTasks.Add(new ProjectTaskGroup(group.Key, group.ToList()));
            }
            IsBusy = false;
        }
    }

    public class ProjectTaskGroup
    {
        public string ProjectName { get; }
        public List<ProjectTask> Tasks { get; }

        public ProjectTaskGroup(string projectName, List<ProjectTask> tasks)
        {
            ProjectName = projectName;
            Tasks = tasks;
        }
    }
}
