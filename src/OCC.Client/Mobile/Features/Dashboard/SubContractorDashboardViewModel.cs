using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class SubContractorDashboardViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IProjectTaskRepository _taskRepository;
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly ITaskAttachmentService _attachmentService;

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _myOverdueTasks = new();

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _myTodayTasks = new();

        [ObservableProperty]
        private int _completionRate;

        public SubContractorDashboardViewModel(
            IAuthService authService,
            IProjectTaskRepository taskRepository,
            IRepository<TaskComment> commentRepository,
            ITaskAttachmentService attachmentService)
        {
            _authService = authService;
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentService = attachmentService;
            Title = "My Assignments";

            WeakReferenceMessenger.Default.Register<EntityUpdatedMessage>(this, async (r, m) =>
            {
                if (m.Value.EntityType == "ProjectTask")
                {
                    await LoadDataAsync();
                }
            });
        }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                var allMyTasks = await _taskRepository.GetMyTasksAsync();
                
                MyOverdueTasks.Clear();
                MyTodayTasks.Clear();

                int completedCount = 0;
                int totalCount = 0;

                foreach (var task in allMyTasks)
                {
                    totalCount++;
                    if (task.IsComplete) completedCount++;

                    var taskVm = new DashboardTaskViewModel(task, _taskRepository, _commentRepository, _attachmentService);
                    
                    if (task.IsOverdue)
                    {
                        MyOverdueTasks.Add(taskVm);
                    }
                    else if (task.FinishDate.Date == DateTime.Today.Date && !task.IsComplete)
                    {
                        MyTodayTasks.Add(taskVm);
                    }
                }

                CompletionRate = totalCount > 0 ? (completedCount * 100) / totalCount : 0;
            }
            catch (Exception)
            {
                // TODO: Log error
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync() => await LoadDataAsync();
    }
}
