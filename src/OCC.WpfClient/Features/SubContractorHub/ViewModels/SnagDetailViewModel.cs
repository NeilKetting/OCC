using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.SubContractorHub.ViewModels
{
    public partial class SnagDetailViewModel : ViewModelBase
    {
        private readonly ISnagService _snagService;
        private readonly IProjectService _projectService;
        private readonly ISubContractorService _subContractorService;
        private readonly IProjectTaskService _taskService;
        private readonly IToastService _toastService;
        private readonly ILogger<SnagDetailViewModel> _logger;

        [ObservableProperty] private SnagJob _snag = new() { Status = SnagStatus.Open, DueDate = DateTime.Now.AddDays(7) };
        [ObservableProperty] private ObservableCollection<Project> _projects = new();
        [ObservableProperty] private ObservableCollection<SubContractor> _subContractors = new();
        [ObservableProperty] private ObservableCollection<ProjectTask> _possibleTasks = new();
        
        [ObservableProperty] private Project? _selectedProject;
        [ObservableProperty] private SubContractor? _selectedSubContractor;
        [ObservableProperty] private ProjectTask? _selectedTask;

        private bool _isEditMode;

        public SnagDetailViewModel(
            ISnagService snagService,
            IProjectService projectService,
            ISubContractorService subContractorService,
            IProjectTaskService taskService,
            IToastService toastService,
            ILogger<SnagDetailViewModel> logger)
        {
            _snagService = snagService;
            _projectService = projectService;
            _subContractorService = subContractorService;
            _taskService = taskService;
            _toastService = toastService;
            _logger = logger;
            Title = "Record Quality Snag";
            
            _ = InitializeAsync();
        }

        public void SetSnag(SnagJob? snag)
        {
            if (snag != null)
            {
                Snag = snag;
                _isEditMode = true;
                Title = "Edit Snag";
                // Pre-select based on IDs
                _ = InitializeEditModeAsync();
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                var projectsTask = _projectService.GetProjectsAsync();
                var subContractorsTask = _subContractorService.GetSubContractorsAsync();
                
                await Task.WhenAll(projectsTask, subContractorsTask);
                
                Projects = new ObservableCollection<Project>(await projectsTask);
                SubContractors = new ObservableCollection<SubContractor>(await subContractorsTask);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task InitializeEditModeAsync()
        {
            await InitializeAsync();
            SelectedProject = Projects.FirstOrDefault(p => p.Id == Snag.ProjectId);
            SelectedSubContractor = SubContractors.FirstOrDefault(s => s.Id == Snag.SubContractorId);
            // Task will be loaded via the SubContractor change event
        }

        partial void OnSelectedSubContractorChanged(SubContractor? value)
        {
            if (value != null)
            {
                Snag.SubContractorId = value.Id;
                _ = LoadPossibleTasksAsync(value.Id);
            }
        }

        private async Task LoadPossibleTasksAsync(Guid subId)
        {
            try
            {
                var tasks = await _taskService.GetSubContractorTasksAsync(subId);
                PossibleTasks = new ObservableCollection<ProjectTask>(tasks);
                
                if (Snag.OriginalTaskId.HasValue)
                {
                    SelectedTask = PossibleTasks.FirstOrDefault(t => t.Id == Snag.OriginalTaskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading possible tasks");
            }
        }

        partial void OnSelectedProjectChanged(Project? value)
        {
            if (value != null) Snag.ProjectId = value.Id;
        }

        partial void OnSelectedTaskChanged(ProjectTask? value)
        {
            if (value != null)
            {
                Snag.OriginalTaskId = value.Id;
                // Auto-fill title if empty
                if (string.IsNullOrWhiteSpace(Snag.Title))
                {
                    Snag.Title = $"Snag: {value.Name}";
                }
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Snag.Title))
            {
                _toastService.ShowError("Validation Error", "Please provide a title for the snag.");
                return;
            }

            if (Snag.ProjectId == Guid.Empty || Snag.SubContractorId == Guid.Empty)
            {
                _toastService.ShowError("Validation Error", "Please select both a project and a partner.");
                return;
            }

            try
            {
                IsBusy = true;
                if (_isEditMode)
                    await _snagService.UpdateSnagJobAsync(Snag);
                else
                    await _snagService.CreateSnagJobAsync(Snag);

                _toastService.ShowSuccess("Success", "Snag recorded successfully");
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving snag");
                _toastService.ShowError("Save Error", "Failed to save snag");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseOverlayMessage());
        }
    }
}
