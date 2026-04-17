using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OCC.Shared.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectDashboardViewModel : ViewModelBase
    {
        #region Private Members

        private List<ProjectTask> _allTasks = new();
        private Project? _project;

        #endregion

        #region Observables - Stats

        [ObservableProperty]
        private int _totalTasks;

        [ObservableProperty]
        private int _completedTasks;

        [ObservableProperty]
        private int _inProgressTasks;

        [ObservableProperty]
        private int _toDoTasks;

        [ObservableProperty]
        private int _overdueTasks;

        [ObservableProperty]
        private double _overallProgress;

        [ObservableProperty]
        private string _projectHealth = "Healthy";

        [ObservableProperty]
        private string _projectHealthColor = "#14B8A6"; // Teal

        [ObservableProperty]
        private string _etaDateString = "N/A";

        [ObservableProperty]
        private string _etaStatus = "ON TRACK";

        [ObservableProperty]
        private string _streetLine1 = string.Empty;

        [ObservableProperty]
        private string _cityStatePostal = string.Empty;

        #endregion

        #region Charts - Status Breakdown

        public ObservableCollection<ISeries> StatusSeries { get; set; } = new();
        public ObservableCollection<ISeries> ScheduleSeries { get; set; } = new();
        
        // Gauge Chart for Progress
        public ObservableCollection<ISeries> ProgressGaugeSeries { get; set; } = new();

        #endregion

        #region Constructor

        public ProjectDashboardViewModel()
        {
            // Initializing with empty series to avoid null refs in UI
            InitializeCharts();
        }

        #endregion

        #region Methods

        public void UpdateProjectData(Project? project, IEnumerable<ProjectTask> tasks)
        {
            _project = project;
            _allTasks = tasks?.ToList() ?? new List<ProjectTask>();
            
            CalculateStats();
            UpdateCharts();
            CalculateETA();

            if (_project != null)
            {
                StreetLine1 = _project.StreetLine1 ?? string.Empty;
                CityStatePostal = $"{_project.City}, {_project.PostalCode}";
            }
            else
            {
                StreetLine1 = string.Empty;
                CityStatePostal = string.Empty;
            }
        }

        private void InitializeCharts()
        {
            StatusSeries.Clear();
            ScheduleSeries.Clear();
            ProgressGaugeSeries.Clear();
        }

        private void CalculateStats()
        {
            if (!_allTasks.Any())
            {
                TotalTasks = 0;
                CompletedTasks = 0;
                InProgressTasks = 0;
                ToDoTasks = 0;
                OverdueTasks = 0;
                OverallProgress = 0;
                return;
            }

            var nonGroupTasks = _allTasks.Where(t => !t.IsGroup).ToList();
            TotalTasks = nonGroupTasks.Count;
            CompletedTasks = nonGroupTasks.Count(t => t.Status == "Completed" || t.Status == "Done");
            InProgressTasks = nonGroupTasks.Count(t => t.Status == "In Progress" || t.Status == "Started" || t.Status == "Halfway" || t.Status == "Almost Done");
            ToDoTasks = nonGroupTasks.Count(t => t.Status == "To Do" || t.Status == "New" || t.Status == "Not Started");

            var now = DateTime.Now;
            OverdueTasks = nonGroupTasks.Count(t => t.Status != "Completed" && t.Status != "Done" && t.FinishDate < now);

            if (TotalTasks > 0)
            {
                OverallProgress = (double)nonGroupTasks.Sum(t => t.PercentComplete) / TotalTasks;
            }

            // Health Calculation
            if (OverdueTasks > 5 || (OverdueTasks > 0 && OverallProgress < 20))
            {
                ProjectHealth = "At Risk";
                ProjectHealthColor = "#EF4444"; // Red
            }
            else if (OverdueTasks > 0)
            {
                ProjectHealth = "Behind Schedule";
                ProjectHealthColor = "#F59E0B"; // Amber
            }
            else
            {
                ProjectHealth = "On Track";
                ProjectHealthColor = "#14B8A6"; // Teal
            }
        }

        private void UpdateCharts()
        {
            // 1. Status Pie Chart
            StatusSeries.Clear();
            AddStatusSeries("Completed", CompletedTasks, SKColors.Teal);
            AddStatusSeries("In Progress", InProgressTasks, SKColors.CornflowerBlue);
            AddStatusSeries("To Do", ToDoTasks, SKColors.SlateGray);

            // 2. Schedule Pie Chart (Health distribution)
            ScheduleSeries.Clear();
            int behind = OverdueTasks;
            int onTrack = TotalTasks - CompletedTasks - OverdueTasks;
            int completed = CompletedTasks;

            AddScheduleSeries("Ahead/Done", completed, SKColors.LightSeaGreen);
            AddScheduleSeries("On Track", onTrack, SKColors.RoyalBlue);
            AddScheduleSeries("Behind", behind, SKColors.IndianRed);

            // 3. Progress Gauge
            ProgressGaugeSeries.Clear();
            ProgressGaugeSeries.Add(new PieSeries<double>
            {
                Values = new double[] { Math.Round(OverallProgress, 1) },
                Name = "Completion",
                InnerRadius = 60,
                Fill = new SolidColorPaint(SKColor.Parse(ProjectHealthColor))
            });
        }

        private void AddStatusSeries(string name, double value, SKColor color)
        {
            if (value <= 0) return;
            StatusSeries.Add(new PieSeries<double>
            {
                Name = name,
                Values = new double[] { value },
                Fill = new SolidColorPaint(color),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsFormatter = p => $"{p.Context.Series.Name}: {p.Coordinate.PrimaryValue}"
            });
        }

        private void AddScheduleSeries(string name, double value, SKColor color)
        {
            if (value <= 0) return;
            ScheduleSeries.Add(new PieSeries<double>
            {
                Name = name,
                Values = new double[] { value },
                Fill = new SolidColorPaint(color)
            });
        }

        private void CalculateETA()
        {
            if (_project == null || OverallProgress <= 0 || OverallProgress >= 100)
            {
                EtaDateString = OverallProgress >= 100 ? "Finished" : "N/A";
                EtaStatus = OverallProgress >= 100 ? "Project Complete" : "Waiting for progress...";
                return;
            }

            try
            {
                var startDate = _project.StartDate;
                var now = DateTime.Now;
                
                if (now <= startDate)
                {
                    EtaDateString = _project.EndDate.ToString("dd MMM yyyy");
                    EtaStatus = "Scheduled (Not Started)";
                    return;
                }

                var timeElapsed = now - startDate;
                var totalEstimatedTimeTicks = timeElapsed.Ticks / (OverallProgress / 100.0);
                var predictedEndDate = startDate.AddTicks((long)totalEstimatedTimeTicks);

                EtaDateString = predictedEndDate.ToString("dd MMM yyyy");
                
                var varianceDays = (predictedEndDate - _project.EndDate).TotalDays;
                if (varianceDays > 7)
                {
                    EtaStatus = $"Expected {Math.Abs(Math.Round(varianceDays))} days late";
                }
                else if (varianceDays < -7)
                {
                    EtaStatus = $"Expected {Math.Abs(Math.Round(varianceDays))} days early";
                }
                else
                {
                    EtaStatus = "On schedule (within 1 week)";
                }
            }
            catch
            {
                EtaDateString = "Error";
                EtaStatus = "Check Project Dates";
            }
        }

        public event EventHandler<IEnumerable<ProjectTask>>? ViewOverdueTasksRequested;

        [RelayCommand]
        private void ViewOverdueTasks()
        {
            if (_allTasks == null) return;
            var now = DateTime.Now;
            var overdue = _allTasks.Where(t => !t.IsGroup && t.Status != "Completed" && t.Status != "Done" && t.FinishDate < now);
            ViewOverdueTasksRequested?.Invoke(this, overdue);
        }

        #endregion
    }
}




