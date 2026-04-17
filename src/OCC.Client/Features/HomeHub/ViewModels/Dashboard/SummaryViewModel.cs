using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.Services;
using OCC.Shared.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OCC.Client.Services.Repositories.ApiServices;
using CommunityToolkit.Mvvm.Messaging;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

using OCC.Client.ViewModels.Messages;
namespace OCC.Client.Features.HomeHub.ViewModels.Dashboard
{
    public partial class SummaryViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<ProjectTask> _taskRepository;
        private readonly ILogger<SummaryViewModel> _logger;

        #endregion

        #region Observables

        #region Observables

        [ObservableProperty] private string _totalProjects = "0";
        [ObservableProperty] private string _activeProjects = "0";
        [ObservableProperty] private string _projectsCompleted = "0";

        // Task Stats
        [ObservableProperty] private int _notStartedCount;
        [ObservableProperty] private int _inProgressCount;
        [ObservableProperty] private int _completedCount;
        [ObservableProperty] private int _totalTaskCount;

        [ObservableProperty] private double _notStartedAngle;       
        [ObservableProperty] private double _inProgressAngle;      
        [ObservableProperty] private double _completedAngle;
        [ObservableProperty] private double _notStartedStartAngle;
        [ObservableProperty] private double _inProgressStartAngle;
        [ObservableProperty] private double _completedStartAngle;

        // To-Do Stats
        [ObservableProperty] private int _todoNotStartedCount;
        [ObservableProperty] private int _todoInProgressCount;
        [ObservableProperty] private int _todoCompletedCount;
        [ObservableProperty] private int _totalTodoCount;

        [ObservableProperty] private double _todoNotStartedAngle;
        [ObservableProperty] private double _todoInProgressAngle;
        [ObservableProperty] private double _todoCompletedAngle;
        [ObservableProperty] private double _todoNotStartedStartAngle;
        [ObservableProperty] private double _todoInProgressStartAngle;
        [ObservableProperty] private double _todoCompletedStartAngle;

        // Time Stats
        [ObservableProperty] private double _totalActualHours;
        [ObservableProperty] private double _totalPlannedHours;
        [ObservableProperty] private double _timeChartAngle;
        [ObservableProperty] private double _timeChartStartAngle = -90;
        [ObservableProperty] private string _timeChartColor = "#22C55E";
        [ObservableProperty] private double _timeEfficiencyPercentage;

        #endregion

        #region Constructors

        public SummaryViewModel(IRepository<Project> projectRepository, IRepository<ProjectTask> taskRepository, ILogger<SummaryViewModel> logger)
        {
            _projectRepository = projectRepository;
            _taskRepository = taskRepository;
            _logger = logger;
            LoadData();

            // Auto-refresh when tasks change
            WeakReferenceMessenger.Default.Register<OCC.Client.ViewModels.Messages.TaskUpdatedMessage>(this, (r, m) => LoadData());
        }

        #endregion

        #region Methods

        private async void LoadData()
        {
            try
            {
                // Projects
                var projects = await _projectRepository.GetAllAsync();
                var projectList = projects.ToList();
                TotalProjects = projectList.Count.ToString();
                ActiveProjects = projectList.Count(p => p.Status == "Active").ToString();
                ProjectsCompleted = projectList.Count(p => p.Status == "Completed").ToString();

                // Tasks & To-Dos
                IEnumerable<ProjectTask> allItems;
                if (_taskRepository is ApiProjectTaskRepository apiRepo)
                {
                    allItems = await apiRepo.GetMyTasksAsync();
                }
                else
                {
                    allItems = await _taskRepository.GetAllAsync();
                }

                var list = allItems.ToList();
                var tasks = list.Where(t => t.Type != TaskType.PersonalToDo).ToList();
                var todos = list.Where(t => t.Type == TaskType.PersonalToDo).ToList();

                var now = DateTime.Now.Date;

                // Process Tasks
                TotalTaskCount = tasks.Count;
                CompletedCount = tasks.Count(t => t.IsComplete);
                InProgressCount = tasks.Count(t => !t.IsComplete && (t.ActualStartDate.HasValue || t.StartDate.Date <= now));
                NotStartedCount = tasks.Count(t => !t.IsComplete && !t.ActualStartDate.HasValue && t.StartDate.Date > now);

                // Process To-Dos
                TotalTodoCount = todos.Count;
                TodoCompletedCount = todos.Count(t => t.IsComplete);
                TodoInProgressCount = todos.Count(t => !t.IsComplete && (t.ActualStartDate.HasValue || t.StartDate.Date <= now));
                TodoNotStartedCount = todos.Count(t => !t.IsComplete && !t.ActualStartDate.HasValue && t.StartDate.Date > now);

                CalculateTimeStatistics(tasks); // Time tracking usually only for main tasks
                CalculateChartAngles();
                CalculateToDoChartAngles();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading summary data");
            }
        }

        #endregion

        #region Helper Methods

        private void CalculateChartAngles()
        {
            if (TotalTaskCount == 0)
            {
                NotStartedAngle = InProgressAngle = CompletedAngle = 0;
                return;
            }

            NotStartedAngle = (double)NotStartedCount / TotalTaskCount * 360;
            InProgressAngle = (double)InProgressCount / TotalTaskCount * 360;
            CompletedAngle = (double)CompletedCount / TotalTaskCount * 360;

            NotStartedStartAngle = -90;
            InProgressStartAngle = NotStartedStartAngle + NotStartedAngle;
            CompletedStartAngle = InProgressStartAngle + InProgressAngle;
        }

        private void CalculateToDoChartAngles()
        {
            if (TotalTodoCount == 0)
            {
                TodoNotStartedAngle = TodoInProgressAngle = TodoCompletedAngle = 0;
                return;
            }

            TodoNotStartedAngle = (double)TodoNotStartedCount / TotalTodoCount * 360;
            TodoInProgressAngle = (double)TodoInProgressCount / TotalTodoCount * 360;
            TodoCompletedAngle = (double)TodoCompletedCount / TotalTodoCount * 360;

            TodoNotStartedStartAngle = -90;
            TodoInProgressStartAngle = TodoNotStartedStartAngle + TodoNotStartedAngle;
            TodoCompletedStartAngle = TodoInProgressStartAngle + TodoInProgressAngle;
        }

        private void CalculateTimeStatistics(List<ProjectTask> allTasks)
        {
            double planned = 0;
            double actual = 0;

            foreach (var t in allTasks)
            {
                if (t.PlannedDurationHours.HasValue)
                    planned += t.PlannedDurationHours.Value.TotalHours;

                if (t.ActualDuration.HasValue)
                    actual += t.ActualDuration.Value.TotalHours;
            }

            TotalPlannedHours = Math.Round(planned, 1);
            TotalActualHours = Math.Round(actual, 1);

            if (TotalPlannedHours > 0)
            {
                double ratio = actual / planned;
                if (ratio > 1.0)
                {
                    TimeChartColor = "#EF4444";
                    TimeChartAngle = 360; 
                    TimeEfficiencyPercentage = 100;
                }
                else
                {
                    TimeChartColor = "#22C55E";
                    TimeChartAngle = ratio * 360;
                    TimeEfficiencyPercentage = ratio * 100;
                }
            }
            else
            {
                TimeChartAngle = 0;
                TimeEfficiencyPercentage = 0;
            }
        }

        #endregion

        #endregion
    }
}
