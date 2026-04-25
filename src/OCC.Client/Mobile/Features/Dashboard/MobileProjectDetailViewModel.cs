using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Mobile.Features.Dashboard
{
    public partial class MobileProjectDetailViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IProjectTaskRepository _taskRepository;
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly ITaskAttachmentService _attachmentService;

        [ObservableProperty]
        private Project? _project;

        [ObservableProperty]
        private ObservableCollection<DashboardTaskViewModel> _tasks = new();

        [ObservableProperty]
        private string _statusMessage = "Loading project...";

        public int Progress => Project?.Tasks?.Any() == true ? (int)Project.Tasks.Average(t => t.PercentComplete) : 0;

        public Guid ProjectId { get; }

        public MobileProjectDetailViewModel(
            Guid projectId,
            IProjectService projectService,
            IProjectTaskRepository taskRepository,
            IRepository<TaskComment> commentRepository,
            ITaskAttachmentService attachmentService)
        {
            ProjectId = projectId;
            _projectService = projectService;
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentService = attachmentService;
            Title = "Project Details";
        }

        public async Task InitializeAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                Project = await _projectService.GetProjectAsync(ProjectId);
                
                if (Project == null)
                {
                    StatusMessage = "Project not found.";
                    return;
                }

                Tasks.Clear();
                if (Project.Tasks != null)
                {
                    foreach (var task in Project.Tasks.OrderBy(t => t.OrderIndex))
                    {
                        Tasks.Add(new DashboardTaskViewModel(task, _taskRepository, _commentRepository, _attachmentService));
                    }
                }

                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigateBackMessage());
        }
    }

    public class NavigateBackMessage { }
}
