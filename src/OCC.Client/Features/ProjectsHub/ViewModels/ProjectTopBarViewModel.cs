using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using OCC.Client.ViewModels.Core;
using OCC.Client.Infrastructure;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using OCC.Shared.Models;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectTopBarViewModel : ViewModelBase, IRecipient<ProjectVariationCountChangedMessage>
    {
        #region Private Members

        private readonly OCC.Client.Services.Interfaces.IPermissionService _permissionService;
        private readonly IProjectManager _projectManager;
        private readonly IDialogService _dialogService;
        private Guid? _originalSiteManagerId;

        #endregion

        public bool CanAccessCalendar => _permissionService != null && _permissionService.CanAccess(NavigationRoutes.Calendar);
        public bool CanDeleteProject => _permissionService != null && _permissionService.CanAccess(NavigationRoutes.Feature_ProjectDeletion);

        public ProjectTopBarViewModel(
            OCC.Client.Services.Interfaces.IPermissionService permissionService,
            IProjectManager projectManager,
            IDialogService dialogService)
        {
            _permissionService = permissionService;
            _projectManager = projectManager;
            _dialogService = dialogService;
            WeakReferenceMessenger.Default.Register(this);
        }

        public ProjectTopBarViewModel()
        {
             _permissionService = null!;
             _projectManager = null!;
             _dialogService = null!;
        }

        #region Events

        public event System.EventHandler? EditProjectRequested;
        public event System.EventHandler? DeleteProjectRequested;

        #endregion

        #region Observables

        [ObservableProperty]
        private string _activeTab = "Dashboard";

        [ObservableProperty]
        private string _projectName = "Engen";

        [ObservableProperty]
        private string _projectIconInitials = "OR";
        
        [ObservableProperty]
        private string _siteManagerInitials = "UA";

        [ObservableProperty]
        private System.Guid _projectId;

        [ObservableProperty]
        private string _projectAddress = string.Empty;

        [ObservableProperty]
        private int _openVariationCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSiteManagers))]
        private ObservableCollection<Employee> _availableSiteManagers = new();

        public bool HasSiteManagers => AvailableSiteManagers.Count > 0;

        [ObservableProperty]
        private Employee? _selectedSiteManager;

        #endregion

        #region Methods



        public void Receive(ProjectVariationCountChangedMessage message)
        {
            if (message.ProjectId == ProjectId)
            {
                OpenVariationCount = message.Count;
            }
        }

        public async Task LoadProjectDataAsync(Project project)
        {
            if (project == null) return;

            ProjectId = project.Id;
            ProjectName = project.Name;
            ProjectAddress = project.FullAddress;
            ProjectIconInitials = GetInitials(project.Name);

            // Load Site Managers
            var managers = await _projectManager.GetSiteManagersAsync();
            AvailableSiteManagers.Clear();
            foreach (var m in managers) AvailableSiteManagers.Add(m);

            // Set Current Site Manager
            _originalSiteManagerId = project.SiteManagerId;
            
            if (project.SiteManagerId.HasValue)
            {
                SelectedSiteManager = managers.FirstOrDefault(m => m.Id == project.SiteManagerId);
                var assigned = managers.FirstOrDefault(m => m.Id == project.SiteManagerId);
                SiteManagerInitials = assigned != null ? GetInitials(assigned.DisplayName) : "UA";
            }
            else
            {
                SelectedSiteManager = null;
                SiteManagerInitials = "UA";
            }
        }

        partial void OnSelectedSiteManagerChanged(Employee? value)
        {
            HandleSiteManagerChange(value);
        }

        private async void HandleSiteManagerChange(Employee? newManager)
        {
            if (ProjectId == Guid.Empty) return;
            
            // If changing to the same or just loading, ignore? 
            // We need to differentiate between "loading" setting the property and "user" setting it.
            // But OnSelectedSiteManagerChanged triggers for both.
            // Check if value actually differs from current project state?
            // "project.SiteManagerId" isn't stored in the VM, only _originalSiteManagerId.
            
            if (newManager?.Id == _originalSiteManagerId) return;
            
            // If we are here, the user changed it (or we programmed it to change).
            // But we also set it in LoadProjectDataAsync. 
            // Wait, LoadProjectDataAsync sets SelectedSiteManager, which triggers this.
            // If newManager.Id == _originalSiteManagerId, we return.
            // So if generic load sets it to original, we return. Correct.
            
            if (newManager != null && _originalSiteManagerId != null && _originalSiteManagerId != Guid.Empty && newManager.Id != _originalSiteManagerId)
            {
                 await _dialogService.ShowAlertAsync("Warning", "The project will not be visible to the current site manager.");
            }

            if (newManager != null)
            {
                await _projectManager.AssignSiteManagerAsync(ProjectId, newManager.Id);
                _originalSiteManagerId = newManager.Id; // user confirmed/accepted, so this is now the new "original"/current
                SiteManagerInitials = GetInitials(newManager.DisplayName);
            }
        }

        private string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "P";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
        }

        [RelayCommand]
        private void EditProject()
        {
            EditProjectRequested?.Invoke(this, System.EventArgs.Empty);
        }

        [RelayCommand]
        private void ProjectSettings()
        {
             // Navigate to project settings
             WeakReferenceMessenger.Default.Send(new OCC.Client.ViewModels.Messages.ProjectSettingsRequestedMessage(ProjectId));
        }

        [RelayCommand]
        private void DeleteProject()
        {
            DeleteProjectRequested?.Invoke(this, System.EventArgs.Empty);
        }

        #endregion
    }
}




