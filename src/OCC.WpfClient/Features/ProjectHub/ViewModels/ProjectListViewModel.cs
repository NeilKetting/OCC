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
    public partial class ProjectListViewModel : ListViewModelBase<ProjectSummaryDto>, IRecipient<ProjectUpdatedMessage>, IRecipient<TaskUpdatedMessage>
    {
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectListViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly IServiceProvider _serviceProvider;
        private readonly LocalSettingsService _settingsService;
        private List<ProjectSummaryDto> _allProjects = new();

        // Column Visibility
        [ObservableProperty] private bool _isProgressVisible = true;
        [ObservableProperty] private bool _isManagerVisible = true;
        [ObservableProperty] private bool _isUpdateVisible = true;
        [ObservableProperty] private bool _isStatusVisible = true;
        [ObservableProperty] private bool _isPriorityVisible = true;

        
        [ObservableProperty] private bool _showDeleted;

        partial void OnShowDeletedChanged(bool value) => _ = LoadDataAsync();
        
        // Link standard commands for centralized UI
        public override IRelayCommand<object> OpenCommand => OpenProjectCommand;
        public override IRelayCommand<object> EditCommand => EditProjectCommand;
        public override IRelayCommand<object> DeleteCommand => DeleteProjectCommand;


        public override string ReportTitle => "Project Portfolio";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Project Name", PropertyName = "Name", Width = 2.5 },
            new() { Header = "Status", PropertyName = "Status", Width = 1 },
            new() { Header = "Manager", PropertyName = "ProjectManager", Width = 1.5 },
            new() { Header = "Progress", PropertyName = "Progress", Width = 1 }
        };



        private readonly ISignalRService? _signalRService;

        public ProjectListViewModel(
            IProjectService projectService,
            ICustomerService customerService,
            IDialogService dialogService,
            ILogger<ProjectListViewModel> logger,
            IToastService toastService,
            IServiceProvider serviceProvider,
            LocalSettingsService settingsService,
            IPdfService pdfService,
            ISignalRService? signalRService = null) : base(pdfService)
        {
            _projectService = projectService;
            _customerService = customerService;
            _dialogService = dialogService;
            _logger = logger;
            _toastService = toastService;
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;
            _signalRService = signalRService;

            Title = "Projects";

            if (_signalRService != null)
            {
                _signalRService.OnProjectChanged += OnProjectChangedReceived;
            }

            LoadLayout();
            _ = LoadDataAsync();
            WeakReferenceMessenger.Default.Register<ProjectUpdatedMessage>(this);
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this);
        }

        private void OnProjectChangedReceived(EntityChangeDto<ProjectSummaryDto> change)
        {
            if (change?.Entity == null) return;
            App.Current?.Dispatcher.Invoke(() =>
            {
                var existing = _allProjects.FirstOrDefault(p => p.Id == change.EntityId || p.Id == change.Entity.Id);
                if (change.Action == "Created" || change.Action == "Create")
                {
                    if (existing == null) _allProjects.Add(change.Entity);
                    else _allProjects[_allProjects.IndexOf(existing)] = change.Entity;
                }
                else if (change.Action == "Updated" || change.Action == "Update")
                {
                    if (existing != null) _allProjects[_allProjects.IndexOf(existing)] = change.Entity;
                    else _allProjects.Add(change.Entity);
                }
                else if (change.Action == "Deleted" || change.Action == "Delete")
                {
                    if (existing != null) _allProjects.Remove(existing);
                }
                FilterItems();
            });
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.ProjectListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsProgressVisible = layout.Columns.FirstOrDefault(c => c.Header == "Progress")?.IsVisible ?? true;
                IsManagerVisible = layout.Columns.FirstOrDefault(c => c.Header == "Manager")?.IsVisible ?? true;
                IsUpdateVisible = layout.Columns.FirstOrDefault(c => c.Header == "Update")?.IsVisible ?? true;
                IsStatusVisible = layout.Columns.FirstOrDefault(c => c.Header == "Status")?.IsVisible ?? true;
                IsPriorityVisible = layout.Columns.FirstOrDefault(c => c.Header == "Priority")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new System.Collections.Generic.List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
                {
                    new() { Header = "Progress", IsVisible = IsProgressVisible },
                    new() { Header = "Manager", IsVisible = IsManagerVisible },
                    new() { Header = "Update", IsVisible = IsUpdateVisible },
                    new() { Header = "Status", IsVisible = IsStatusVisible },
                    new() { Header = "Priority", IsVisible = IsPriorityVisible }
                }
            };
            _settingsService.Settings.ProjectListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsProgressVisibleChanged(bool value) => SaveLayout();
        partial void OnIsManagerVisibleChanged(bool value) => SaveLayout();
        partial void OnIsUpdateVisibleChanged(bool value) => SaveLayout();
        partial void OnIsStatusVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPriorityVisibleChanged(bool value) => SaveLayout();

        

        public override async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var projects = (await _projectService.GetProjectSummariesAsync(ShowDeleted)).OrderBy(p => p.Name).ToList();

                if (projects.Count > 100)
                {
                    // Step 1: Fast render top 100 records
                    _allProjects = projects.Take(100).ToList();
                    FilterItems();
                    IsBusy = false; // Unblock UI

                    // Step 2: Background hydration of full dataset
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            _allProjects = projects;
                            FilterItems();
                        });
                    });
                }
                else
                {
                    _allProjects = projects;
                    FilterItems();
                }
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
            var vm = _serviceProvider.GetRequiredService<ProjectCreateDetailViewModel>();
            OpenOverlay(vm, (res) => _ = LoadDataAsync());
        }



        [RelayCommand]
        private void OpenProject(object? parameter)
        {
            var target = parameter as ProjectSummaryDto ?? SelectedItem;
            if (target == null) return;
            WeakReferenceMessenger.Default.Send(new OpenProjectMessage(target.Id));
        }

        [RelayCommand]
        private async Task EditProject(object? parameter)
        {
                        var target = parameter as ProjectSummaryDto ?? SelectedItem;
            if (target == null) return;


            var vm = _serviceProvider.GetRequiredService<ProjectEditDetailViewModel>();
            await vm.InitializeAsync(target.Id);
            OpenOverlay(vm, (res) => _ = LoadDataAsync());
        }

        [RelayCommand]
        private async Task DeleteProject(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            if (targets.Count == 1)
            {
                var project = targets[0];
                string title = project.IsActive ? "Delete Project" : "Permanently Delete Project";
                string message = project.IsActive
                    ? $"Are you sure you want to delete '{project.Name}'? It can be restored from the archive later."
                    : $"Are you sure you want to PERMANENTLY delete '{project.Name}'?\n\n" +
                      "This will delete EVERYTHING linked to this project, including:\n" +
                      "• All Tasks and Task Comments\n" +
                      "• All Team Member Assignments\n" +
                      "• All HSEQ Documents and Audits\n" +
                      "• All Incidents and Snag Jobs\n" +
                      "• All Variation Orders\n\n" +
                      "THIS ACTION CANNOT BE UNDONE.";

                var confirm = await _dialogService.ShowConfirmationAsync(title, message);
                if (!confirm) return;

                IsBusy = true;
                try
                {
                    await _projectService.DeleteProjectAsync(project.Id, !project.IsActive);
                    _toastService.ShowSuccess("Success", project.IsActive ? "Project deleted (Archived)." : "Project permanently removed.");
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
            else
            {
                string message = $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?";
                var confirm = await _dialogService.ShowConfirmationAsync("Delete Multiple Projects", message);

                if (confirm)
                {
                    IsBusy = true;
                    try
                    {
                        foreach (var p in targets)
                        {
                            await _projectService.DeleteProjectAsync(p.Id, false);
                        }
                        _toastService.ShowSuccess("Success", $"{targets.Count} projects archived.");
                        await LoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during bulk delete");
                        _toastService.ShowError("Error", "Some projects could not be deleted.");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task RestoreProject(ProjectSummaryDto project)
        {
            if (project == null) return;

            IsBusy = true;
            try
            {
                await _projectService.RestoreProjectAsync(project.Id);
                _toastService.ShowSuccess("Restored", $"Project '{project.Name}' has been restored.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring project {Id}", project.Id);
                _toastService.ShowError("Error", "Failed to restore project.");
            }
            finally
            {
                IsBusy = false;
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
