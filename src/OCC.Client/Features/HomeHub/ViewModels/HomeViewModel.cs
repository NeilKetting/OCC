using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services;
using OCC.Client.Features.HomeHub.ViewModels.Dashboard;
using OCC.Client.Features.HomeHub.ViewModels.MySummary;
using OCC.Client.Features.HomeHub.ViewModels.Shared;
using OCC.Client.Features.TaskHub.ViewModels;
using OCC.Client.ViewModels.Messages;
using OCC.Client.Features.ProjectsHub.ViewModels;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using Microsoft.Extensions.DependencyInjection;

namespace OCC.Client.Features.HomeHub.ViewModels
{
    public partial class HomeViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IAuthService _authService;
        private readonly ITimeService _timeService;
        private readonly IProjectTaskRepository _projectTaskRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Customer> _customerRepository;
        private readonly IRepository<ProjectTask> _projectTaskModelRepository;
        private readonly IRepository<AppSetting> _appSettingsRepository;
        private readonly IRepository<Employee> _staffRepository;
        private readonly IRepository<TaskAssignment> _taskAssignmentRepository;
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Team> _teamRepository;
        private readonly IDialogService _dialogService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ITaskAttachmentService _attachmentService;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Observables

        [ObservableProperty]
        private HomeMenuViewModel _homeMenu;

        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private MySummaryPageViewModel _mySummaryPage;

        [ObservableProperty]
        private TaskListViewModel _taskList;

        [ObservableProperty]
        private string _greeting = string.Empty;

        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dd MMMM yyyy");

        [ObservableProperty]
        private bool _isTaskDetailVisible = false;

        [ObservableProperty]
        private TaskDetailViewModel? _currentTaskDetail;

        [ObservableProperty]
        private bool _isCreateProjectVisible;

        [ObservableProperty]
        private CreateProjectViewModel? _createProjectVM;

        #endregion

        #region Properties

        public bool IsTopBarVisible => true;

        #endregion

        #region Constructors

        public HomeViewModel()
        {
            // Parameterless constructor for design-time support
            Greeting = "Good day, User";
            _homeMenu = null!;
            _mySummaryPage = null!;
            _taskList = null!;
            _currentView = null!;
            _authService = null!;
            _timeService = null!;
            _projectTaskRepository = null!;
            _projectRepository = null!;
            _customerRepository = null!;
            _projectTaskModelRepository = null!;
            _appSettingsRepository = null!;
            _staffRepository = null!;
            _taskAssignmentRepository = null!;
            _commentRepository = null!;
            _userRepository = null!;
            _teamRepository = null!;
            _dialogService = null!;
            _loggerFactory = null!;
            _loggerFactory = null!;
            _serviceProvider = null!;
            _attachmentService = null!;
        }

        public HomeViewModel(HomeMenuViewModel homeMenu,
                             SummaryViewModel mySummary,
                             TasksWidgetViewModel myTasks,
                             IAuthService authService,
                             ITimeService timeService,
                             IProjectTaskRepository projectTaskRepository,
                             IRepository<Project> projectRepository,
                             IRepository<Customer> customerRepository,
                             IRepository<ProjectTask> projectTaskModelRepository,
                             IRepository<AppSetting> appSettingsRepository,
                             IRepository<Employee> staffRepository,
                             IRepository<TaskAssignment> taskAssignmentRepository,
                             IRepository<TaskComment> commentRepository,
                             IRepository<User> userRepository,
                             IRepository<Team> teamRepository,
                             IDialogService dialogService,
                             ILoggerFactory loggerFactory,
                             ITaskAttachmentService attachmentService,
                             IServiceProvider serviceProvider)
        {
            _authService = authService;
            _currentView = null!; // Silence warning, set in Initialize()
            _timeService = timeService;
            _projectTaskRepository = projectTaskRepository;
            _projectRepository = projectRepository;
            _customerRepository = customerRepository;
            _projectTaskModelRepository = projectTaskModelRepository;
            _serviceProvider = serviceProvider;
            _appSettingsRepository = appSettingsRepository;
            _staffRepository = staffRepository;
            _taskAssignmentRepository = taskAssignmentRepository;
            _commentRepository = commentRepository;
            _userRepository = userRepository;
            _teamRepository = teamRepository;
            _dialogService = dialogService;
            _loggerFactory = loggerFactory;
            _attachmentService = attachmentService;

            HomeMenu = homeMenu;
            
            // Initialize Pages
            MySummaryPage = new MySummaryPageViewModel(mySummary, myTasks);
            TaskList = new TaskListViewModel(_projectTaskRepository, _loggerFactory.CreateLogger<TaskListViewModel>(), _dialogService);
            TaskList.MyTasksOnly = true;
            
            // Subscribe to selection
            TaskList.TaskSelectionRequested += async (s, idString) => 
            {
                if (Guid.TryParse(idString, out var guid))
                    await OpenTaskDetail(guid);
            };

            WeakReferenceMessenger.Default.Register<CreateProjectMessage>(this, (r, m) => OpenCreateProject());
            WeakReferenceMessenger.Default.Register<TaskSelectedMessage>(this, async (r, m) => await OpenTaskDetail(m.TaskId));

            HomeMenu.PropertyChanged += HomeMenu_PropertyChanged;

            Initialize();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task OpenTaskDetail(Guid taskId)
        {
            CurrentTaskDetail = new TaskDetailViewModel(_projectTaskRepository, _staffRepository, _teamRepository, _userRepository, _projectRepository, _taskAssignmentRepository, _commentRepository, _attachmentService, _dialogService, _authService);
            CurrentTaskDetail.CloseRequested += (s, e) => CloseTaskDetail();
            await CurrentTaskDetail.LoadTaskById(taskId);
            IsTaskDetailVisible = true;
        }

        [RelayCommand]
        private void CloseTaskDetail()
        {
            IsTaskDetailVisible = false;
            CurrentTaskDetail = null;
        }

        #endregion

        #region Helper Methods

        private void Initialize()
        {
            var now = DateTime.Now;
            Greeting = GetGreeting(now);
            CurrentDate = now.ToString("dd MMMM yyyy");
            
            // Set default view
            UpdateVisibility();
        }

        private void HomeMenu_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeMenuViewModel.ActiveTab))
            {
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            switch (HomeMenu.ActiveTab)
            {
                case "List":
                    CurrentView = TaskList;
                    break;
                case "My Summary":
                default:
                    CurrentView = MySummaryPage;
                    break;
            }
        }

        private async void CreateNewTask()
        {
            var newTask = new ProjectTask
            {
                Name = "New Task",
                Description = "",
            };

            await _projectTaskRepository.AddAsync(newTask);
            await OpenTaskDetail(newTask.Id);
        }

        private string GetGreeting(DateTime time)
        {
            string timeGreeting = time.Hour < 12 ? "Good morning" :
                                  time.Hour < 18 ? "Good afternoon" : "Good evening";

            var userName = _authService.CurrentUser?.DisplayName ?? "User";
            return $"{timeGreeting}, {userName}";
        }

        private async void OpenNewTaskPopup(Guid? projectId = null, DateTime? initialDate = null)
        {
            CurrentTaskDetail = new TaskDetailViewModel(_projectTaskRepository, _staffRepository, _teamRepository, _userRepository, _projectRepository, _taskAssignmentRepository, _commentRepository, _attachmentService, _dialogService, _authService);
            CurrentTaskDetail.CloseRequested += (s, e) => CloseTaskDetail();
            await CurrentTaskDetail.InitializeForCreation(projectId, null, initialDate);
            IsTaskDetailVisible = true;
        }

        private void OpenCreateProject()
        {
            CreateProjectVM = _serviceProvider.GetRequiredService<CreateProjectViewModel>();
            CreateProjectVM.CloseRequested += (s, e) => CloseCreateProject();
            CreateProjectVM.ProjectCreated += ProjectCreatedHandler;
            IsCreateProjectVisible = true;
        }

        private void CloseCreateProject()
        {
            IsCreateProjectVisible = false;
            CreateProjectVM = null;
        }

        private void ProjectCreatedHandler(object? sender, Guid projectId)
        {
        }

        #endregion
    }
}
