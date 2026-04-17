using OCC.Client.Features.TaskHub.Views;
using OCC.Client.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectTaskListViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<ProjectTask> _tasks = new();

        [ObservableProperty]
        private ProjectTask? _selectedTask;

        public event EventHandler<Guid>? TaskSelectionRequested;
        public event EventHandler? ToggleExpandRequested;

        public bool HasTasks => Tasks.Count > 0;

        [RelayCommand]
        private void ToggleExpand(ProjectTask task)
        {
            ToggleExpandRequested?.Invoke(this, EventArgs.Empty);
        }

        partial void OnSelectedTaskChanged(ProjectTask? value)
        {
            if (value != null)
            {
                TaskSelectionRequested?.Invoke(this, value.Id);
            }
        }

        public void UpdateTasks(IEnumerable<ProjectTask> tasks)
        {
            var newList = tasks.ToList();
            var previousSelectedId = SelectedTask?.Id;

            // 1. Remove items no longer present
            var toRemove = Tasks.Where(t => !newList.Any(n => n.Id == t.Id)).ToList();
            foreach (var item in toRemove) Tasks.Remove(item);

            // 2. Add or Update items
            for (int i = 0; i < newList.Count; i++)
            {
                var newTask = newList[i];
                if (i < Tasks.Count)
                {
                    if (Tasks[i].Id == newTask.Id)
                    {
                        // Update if state changed - since ProjectTask auto-properties don't notify, 
                        // we replace the object if key properties differ.
                        if (Tasks[i].Status != newTask.Status || 
                            Tasks[i].Priority != newTask.Priority ||
                            Tasks[i].PercentComplete != newTask.PercentComplete ||
                            Tasks[i].Name != newTask.Name ||
                            Tasks[i].AssigneeInitials != newTask.AssigneeInitials ||
                            Tasks[i].FinishDate != newTask.FinishDate)
                        {
                            Tasks[i] = newTask;
                        }
                    }
                    else
                    {
                        Tasks.Insert(i, newTask);
                    }
                }
                else
                {
                    Tasks.Add(newTask);
                }
            }

            // 3. Restore selection based on ID
            if (previousSelectedId.HasValue)
            {
                var newSelected = Tasks.FirstOrDefault(t => t.Id == previousSelectedId.Value);
                if (newSelected != null)
                {
                    // Selection might have been cleared by collection changes, so re-set it
                    SelectedTask = newSelected;
                }
            }

            OnPropertyChanged(nameof(HasTasks));
        }
    }
}





