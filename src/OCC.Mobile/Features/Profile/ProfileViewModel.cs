using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Mobile.Features.Profile
{
    public partial class ProfileViewModel : ViewModelBase, IDisposable
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly IUpdateService? _updateService;
        private readonly IProjectService _projectService;
        private readonly IProjectTaskService _taskService;
        private readonly ISignalRService _signalRService;

        [ObservableProperty]
        private User? _currentUser;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private int _doneTodayCount;

        [ObservableProperty]
        private int _projectsCount;

        [ObservableProperty]
        private int _tasksTotalCount;

        public string Initials
        {
            get
            {
                if (CurrentUser == null) return "JD";
                var first = !string.IsNullOrWhiteSpace(CurrentUser.FirstName) ? CurrentUser.FirstName[0].ToString() : "";
                var last = !string.IsNullOrWhiteSpace(CurrentUser.LastName) ? CurrentUser.LastName[0].ToString() : "";
                var initials = (first + last).ToUpper();
                return string.IsNullOrEmpty(initials) ? "JD" : initials;
            }
        }

        public string LocationDisplay => CurrentUser?.Location ?? "Cape Town";

        partial void OnCurrentUserChanged(User? value)
        {
            OnPropertyChanged(nameof(Initials));
            OnPropertyChanged(nameof(LocationDisplay));
        }

        public ProfileViewModel(
            IAuthService authService, 
            INavigationService navigationService, 
            IProjectService projectService,
            IProjectTaskService taskService,
            ISignalRService signalRService,
            IUpdateService? updateService = null)
        {
            _authService = authService;
            _navigationService = navigationService;
            _projectService = projectService;
            _taskService = taskService;
            _signalRService = signalRService;
            _updateService = updateService;
            
            CurrentUser = _authService.CurrentUser;
            AppVersion = _updateService?.CurrentVersion ?? "1.0.0";
            Title = "My Profile";

            _signalRService.EntityUpdated += OnEntityUpdated;

            LoadStatsAsync().FireAndForget();
        }

        private void OnEntityUpdated(string entityType, string action, Guid id)
        {
            if (entityType == "Project" || entityType == "ProjectTask")
            {
                LoadStatsAsync().FireAndForget();
            }
        }

        public override void Dispose()
        {
            _signalRService.EntityUpdated -= OnEntityUpdated;
            base.Dispose();
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var projects = await _projectService.GetProjectsAsync(assignedToMe: true);
                var projectList = projects.GroupBy(p => p.Id).Select(g => g.First()).ToList();
                ProjectsCount = projectList.Count;

                var tasks = await _taskService.GetTasksAsync(projectId: null, assignedToMe: true, skip: 0, take: 1000);
                var taskList = tasks.ToList();
                TasksTotalCount = taskList.Count;

                DoneTodayCount = taskList.Count(t => t.IsComplete && 
                    ((t.ActualCompleteDate.HasValue && t.ActualCompleteDate.Value.ToLocalTime().Date == DateTime.Today) ||
                     (t.UpdatedAtUtc.HasValue && t.UpdatedAtUtc.Value.ToLocalTime().Date == DateTime.Today)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load profile stats: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CheckForUpdates()
        {
            WeakReferenceMessenger.Default.Send(UpdateCheckMessage.Instance);
        }

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            _navigationService.NavigateTo<Login.LoginViewModel>();
        }
    }
}
