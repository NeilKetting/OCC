using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectOverdueTasksViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<ProjectTask> _overdueTasks = new();

        public event EventHandler? BackRequested;

        public bool HasTasks => OverdueTasks.Count > 0;

        public void LoadTasks(IEnumerable<ProjectTask> tasks)
        {
            var now = DateTime.Now;
            var overdue = tasks.Where(t => !t.IsGroup && t.Status != "Completed" && t.FinishDate < now)
                               .OrderBy(t => t.FinishDate)
                               .ToList();

            OverdueTasks.Clear();
            foreach (var task in overdue)
            {
                OverdueTasks.Add(task);
            }

            OnPropertyChanged(nameof(HasTasks));
        }

        [RelayCommand]
        private void GoBack()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<ProjectTask>? TaskSelectionRequested;

        [RelayCommand]
        private void SelectTask(ProjectTask? task)
        {
            if (task != null)
            {
                TaskSelectionRequested?.Invoke(this, task);
            }
        }
    }
}
