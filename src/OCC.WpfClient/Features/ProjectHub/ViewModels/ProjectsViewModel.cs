using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OCC.Shared.Interfaces;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectsViewModel : ListViewModelBase<ProjectSummaryDto>, IRecipient<ProjectUpdatedMessage>, IRecipient<TaskUpdatedMessage>
    {
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectsViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly IServiceProvider _serviceProvider;
        private readonly LocalSettingsService _settingsService;
        private List<ProjectSummaryDto> _allProjects = new();

        // Column Visibility
        [ObservableProperty] private bool _isProgressVisible = true;
        [ObservableProperty] private bool _isManagerVisible = true;
        [ObservableProperty] private bool _isUpdateVisible = true;
        [ObservableProperty] private bool _isStatusVisible = true;

        [ObservableProperty] private bool _isColumnPickerOpen;


        public override string ReportTitle => "Project Portfolio";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Project Name", PropertyName = "Name", Width = 2.5 },
            new() { Header = "Status", PropertyName = "Status", Width = 1 },
            new() { Header = "Manager", PropertyName = "ProjectManager", Width = 1.5 },
            new() { Header = "Progress", PropertyName = "Progress", Width = 1 }
        };



        public ProjectsViewModel(
            IProjectService projectService,
            ICustomerService customerService,
            IDialogService dialogService,
            ILogger<ProjectsViewModel> logger,
            IToastService toastService,
            IServiceProvider serviceProvider,
            LocalSettingsService settingsService,
            IPdfService pdfService) : base(pdfService)
        {
            _projectService = projectService;
            _customerService = customerService;
            _dialogService = dialogService;
            _logger = logger;
            _toastService = toastService;
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;

            Title = "Projects";
            LoadLayout();
            _ = LoadDataAsync();
            WeakReferenceMessenger.Default.Register<ProjectUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this);
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.ProjectsListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsProgressVisible = layout.Columns.FirstOrDefault(c => c.Header == "Progress")?.IsVisible ?? true;
                IsManagerVisible = layout.Columns.FirstOrDefault(c => c.Header == "Manager")?.IsVisible ?? true;
                IsUpdateVisible = layout.Columns.FirstOrDefault(c => c.Header == "Update")?.IsVisible ?? true;
                IsStatusVisible = layout.Columns.FirstOrDefault(c => c.Header == "Status")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new System.Collections.Generic.List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "Progress", IsVisible = IsProgressVisible },
                    new() { Header = "Manager", IsVisible = IsManagerVisible },
                    new() { Header = "Update", IsVisible = IsUpdateVisible },
                    new() { Header = "Status", IsVisible = IsStatusVisible }
                }
            };
            _settingsService.Settings.ProjectsListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsProgressVisibleChanged(bool value) => SaveLayout();
        partial void OnIsManagerVisibleChanged(bool value) => SaveLayout();
        partial void OnIsUpdateVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStatusVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public override async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var projects = await _projectService.GetProjectSummariesAsync();
                _allProjects = projects.OrderBy(p => p.Name).ToList();
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project summaries");
                _toastService.ShowError("Error", $"Failed to load projects: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddProject()
        {
            if (OverlayViewModel != null) return;

            var vm = new CreateProjectViewModel(
                _projectService,
                _customerService,
                _serviceProvider.GetRequiredService<IEmployeeService>(),
                _serviceProvider.GetRequiredService<IUserService>(),
                _serviceProvider.GetRequiredService<IGoogleMapsService>(),
                _serviceProvider.GetRequiredService<ISubContractorService>(),
                _serviceProvider.GetRequiredService<ISettingsService>(),
                _toastService,
                _serviceProvider.GetRequiredService<OCC.WpfClient.Services.Infrastructure.ConnectionSettings>());

            vm.CloseRequested += (s, e) => CloseOverlay();
            vm.ProjectCreated += (s, id) => { 
                CloseOverlay();
                _ = LoadDataAsync();
            };

            OpenOverlay(vm);
        }



        [RelayCommand]
        private void OpenProject(ProjectSummaryDto project)
        {
            if (project == null) return;
            WeakReferenceMessenger.Default.Send(new OpenProjectMessage(project.Id));
        }

        [RelayCommand]
        private void EditProject(ProjectSummaryDto project)
        {
            if (project == null) return;
            // TODO: Open EditProjectDialog
            _toastService.ShowInfo("Upcoming", $"Editing {project.Name} is coming soon.");
        }

        [RelayCommand]
        private async Task DeleteProject(ProjectSummaryDto project)
        {
            if (project == null) return;
            var confirm = await _dialogService.ShowConfirmationAsync("Delete Project", 
                $"Are you sure you want to delete '{project.Name}'? This action cannot be undone.");
            
            if (confirm)
            {
                IsBusy = true;
                try
                {
                    await _projectService.DeleteProjectAsync(project.Id);
                    _toastService.ShowSuccess("Deleted", "Project deleted successfully.");
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting project {Id}", project.Id);
                    _toastService.ShowError("Error", "Failed to delete project. Please try again.");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        protected override void FilterItems()
        {
            var filtered = _allProjects.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(p => 
                    (p.Name?.ToLower().Contains(query) ?? false) ||
                    (p.ProjectManager?.ToLower().Contains(query) ?? false) ||
                    (p.SiteManagerName?.ToLower().Contains(query) ?? false));
            }

            Items = new ObservableCollection<ProjectSummaryDto>(filtered.ToList());
            TotalCount = Items.Count;
        }


        public void Receive(ProjectUpdatedMessage message)
        {
            _ = LoadDataAsync();
        }

        public void Receive(TaskUpdatedMessage message)
        {
            // Task updates usually trigger project progress rollups, so we refresh the whole list
            _ = LoadDataAsync();
        }
    }
}
