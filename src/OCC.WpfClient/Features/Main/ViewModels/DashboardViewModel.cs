using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Linq;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    /// <summary>
    /// ViewModel for the main dashboard view, displaying metrics like active task counts,
    /// todo counts, user counts, and general welcome greetings.
    /// </summary>
    public partial class DashboardViewModel : ViewModelBase
    {
        #region Private Fields

        // Manages user querying
        private readonly IUserService _userService;

        // Handles system toast notifications
        private readonly IToastService _toastService;

        // Provides current authenticated user details
        private readonly IAuthService _authService;

        // Retrieves project and personal tasks
        private readonly IProjectTaskService _taskService;

        // Manages employee querying (specifically birthdays)
        private readonly IEmployeeService _employeeService;

        #endregion

        #region Properties & Observables

        // Display name of the logged-in user
        [ObservableProperty]
        private string _userName = "User";

        // Count of active/uncompleted tasks assigned to the user
        [ObservableProperty]
        private int _taskCount;

        // Count of personal todo items remaining for the user
        [ObservableProperty]
        private int _todoCount;

        // Total count of registered users in the system
        [ObservableProperty]
        private int _userCount;

        // The current calendar date formatted for display
        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dd MMMM yyyy");

        // The welcome greeting adjusted to the time of day
        [ObservableProperty]
        private string _greeting = "Good day";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the dashboard, setting the username, determining the time-of-day greeting,
        /// and triggering background data loading.
        /// </summary>
        public DashboardViewModel(
            IUserService userService, 
            IToastService toastService, 
            IAuthService authService,
            IProjectTaskService taskService,
            IEmployeeService employeeService)
        {
            _userService = userService;
            _toastService = toastService;
            _authService = authService;
            _taskService = taskService;
            _employeeService = employeeService;
            Title = "Dashboard";
            
            UserName = _authService.CurrentUser?.DisplayName ?? "User";
            Greeting = GetGreeting();
            _ = LoadData();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads user count and user task statistics from services.
        /// </summary>
        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                var users = await _userService.GetUsersAsync();
                UserCount = users.Count();

                var tasks = await _taskService.GetTasksAsync(assignedToMe: true);
                var taskList = tasks.ToList();
                
                TaskCount = taskList.Count(t => !t.IsComplete);
                TodoCount = taskList.Count(t => t.Type == TaskType.PersonalToDo && !t.IsComplete);

                _toastService.ShowSuccess("System Active", "Toast Notification System is now live!");

                // Check for employee birthdays today and trigger sticky toasts
                try
                {
                    var today = DateTime.Today;
                    var employees = await _employeeService.GetEmployeesAsync();
                    var birthdayEmployees = employees
                        .Where(e => e.Status == EmployeeStatus.Active &&
                                    e.DoB.Month == today.Month &&
                                    e.DoB.Day == today.Day)
                        .ToList();

                    foreach (var emp in birthdayEmployees)
                    {
                        _toastService.ShowInfo("Employee Birthday 🎉", $"Happy Birthday to {emp.DisplayName}! 🎂", isSticky: true);
                    }
                }
                catch (Exception ex)
                {
                    // Non-critical background failure
                }
            }
            catch 
            { 
                // Fallback / Ignore background load failures silently
            }
        }

        /// <summary>
        /// Dynamically calculates the greeting based on the current hour of the day.
        /// </summary>
        private string GetGreeting()
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12) return "Good morning";
            if (hour < 18) return "Good afternoon";
            return "Good evening";
        }

        #endregion
    }
}
