using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Measure;
using OCC.Shared.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.DTOs;
using OCC.Shared.Enums;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectSpecificDashboardViewModel : ViewModelBase
    {
        private readonly Services.Interfaces.ISnagService _snagService;
        private readonly Services.Interfaces.IAttendanceService _attendanceService;
        private readonly Services.Interfaces.IProjectVariationOrderService _voService;
        private readonly Services.Interfaces.IProjectReportService _projectReportService;
        private List<ProjectTask> _allTasks = new();
        private Project? _project;

        [ObservableProperty] private int _totalTasks;
        [ObservableProperty] private int _completedTasks;
        [ObservableProperty] private int _inProgressTasks;
        [ObservableProperty] private int _toDoTasks;
        [ObservableProperty] private int _delayedStartTasks;
        [ObservableProperty] private int _overdueTasks;
        [ObservableProperty] private double _overallProgress;
        [ObservableProperty] private string _projectHealth = "Healthy";
        [ObservableProperty] private string _projectHealthColor = "#14B8A6"; // Teal
        [ObservableProperty] private string _etaDateString = "N/A";
        [ObservableProperty] private string _etaStatus = "ON TRACK";
        [ObservableProperty] private string _streetLine1 = string.Empty;
        [ObservableProperty] private string _cityStatePostal = string.Empty;
        [ObservableProperty] private string _varianceText = string.Empty;
        [ObservableProperty] private bool _isLate;
        [ObservableProperty] private double _safeWorkingHours;
        [ObservableProperty] private int _activeVoCount;
        [ObservableProperty] private string _reportStatusSummary = "No report summary generated yet.";
        
        [ObservableProperty] private ObservableCollection<ProjectTask> _upcomingMilestones = new();
        [ObservableProperty] private int _upcomingMilestonesCount;
        [ObservableProperty] private int _activeSnagsCount;
        [ObservableProperty] private ObservableCollection<SubContractorSummaryDto> _subContractors = new();
        
        public SolidColorPaint LegendTextPaint { get; } = new SolidColorPaint(SKColors.White);

        public ObservableCollection<ISeries> StatusSeries { get; set; } = new();
        public ObservableCollection<ISeries> ScheduleSeries { get; set; } = new();
        public ObservableCollection<ISeries> ProgressGaugeSeries { get; set; } = new();

        public override void Dispose()
        {
            base.Dispose();
            StatusSeries.Clear();
            ScheduleSeries.Clear();
            ProgressGaugeSeries.Clear();
            UpcomingMilestones.Clear();
            SubContractors.Clear();
            _allTasks.Clear();
        }

        public ProjectSpecificDashboardViewModel(
            Services.Interfaces.ISnagService snagService,
            Services.Interfaces.IAttendanceService attendanceService,
            Services.Interfaces.IProjectVariationOrderService voService,
            Services.Interfaces.IProjectReportService projectReportService)
        {
            _snagService = snagService;
            _attendanceService = attendanceService;
            _voService = voService;
            _projectReportService = projectReportService;
            Title = "Stats";
        }

        public void UpdateProjectData(Project? project, IEnumerable<ProjectTask> tasks)
        {
            App.Current.Dispatcher.Invoke(() =>
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
                    ExtractSubContractors(_allTasks);
                    
                    // Filter upcoming milestones (next 7 days)
                    var now = DateTime.Now;
                    var nextWeek = now.AddDays(7);
                    var milestones = _allTasks
                        .Where(t => !t.IsComplete && t.FinishDate >= now && t.FinishDate <= nextWeek)
                        .OrderBy(t => t.FinishDate)
                        .ToList();
                        
                    UpcomingMilestones.Clear();
                    foreach (var m in milestones) UpcomingMilestones.Add(m);
                    UpcomingMilestonesCount = milestones.Count;
                    
                    _ = FetchSnagData();
                    _ = FetchProjectSpecificHseqData();
                    _ = FetchProjectReportDraftData();
                }
            });
        }

        private async Task FetchProjectSpecificHseqData()
        {
            if (_project == null) return;
            var projectId = _project.Id;

            try
            {
                SafeWorkingHours = await _attendanceService.GetProjectSafeHoursAsync(projectId);
            }
            catch
            {
                SafeWorkingHours = 0;
            }

            try
            {
                var vos = await _voService.GetVariationOrdersAsync(projectId);
                ActiveVoCount = vos.Count(v => v.Status == "Approved");
            }
            catch
            {
                ActiveVoCount = 0;
            }
        }

        private async Task FetchProjectReportDraftData()
        {
            if (_project == null) return;
            var projectId = _project.Id;
            try
            {
                var draft = await _projectReportService.GetDraftAsync(projectId);
                ReportStatusSummary = draft?.StatusSummary ?? "No report summary generated yet.";
            }
            catch
            {
                ReportStatusSummary = "Failed to load status summary.";
            }
        }

        private async Task FetchSnagData()
        {
            if (_project == null) return;
            try
            {
                var snags = await _snagService.GetProjectSnagJobsAsync(_project.Id);
                ActiveSnagsCount = snags.Count(s => s.Status != SnagStatus.Fixed && s.Status != SnagStatus.Closed);
            }
            catch { /* Ignore for now */ }
        }

        private void ExtractSubContractors(IEnumerable<ProjectTask> tasks)
        {
            if (tasks == null) return;

            // In a real app, we'd probably have a direct link or fetch them from the assignments.
            // For now, let's extract unique names from assignments of type Contractor
            // and assume we'll display them. Since we don't have the full objects here, 
            // we'll just create dummy ones for the visual demo.
            var contractorsInTasks = tasks
                .SelectMany(t => t.Assignments ?? new List<TaskAssignment>())
                .Where(a => a.AssigneeType == AssigneeType.Contractor)
                .Select(a => a.AssigneeName)
                .Distinct()
                .ToList();

            // Clear and add
            SubContractors.Clear();
            foreach (var name in contractorsInTasks)
            {
                SubContractors.Add(new SubContractorSummaryDto 
                { 
                    Name = name,
                    // In a real scenario, we'd look up the color from the database.
                    // For the demo, we'll generate or use a default.
                    ColorTheme = "#1D4ED8" 
                });
            }
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
            CompletedTasks = nonGroupTasks.Count(t => t.IsComplete);
            InProgressTasks = nonGroupTasks.Count(t => !t.IsComplete && (t.PercentComplete > 0 || t.Status == "In Progress" || t.Status == "Started" || t.Status == "Halfway" || t.Status == "Almost Done" || (t.Status != "Not Started" && t.Status != "To Do" && t.Status != "New" && t.Status != "On Hold" && t.Status != "Cancelled")));
            ToDoTasks = nonGroupTasks.Count(t => !t.IsComplete && t.PercentComplete == 0 && (t.Status == "Not Started" || t.Status == "To Do" || t.Status == "New"));

            var now = DateTime.Now;
            OverdueTasks = nonGroupTasks.Count(t => t.IsOverdue);
            DelayedStartTasks = nonGroupTasks.Count(t => !t.IsComplete && t.Status == "Not Started" && t.StartDate < now && t.FinishDate >= now);

            if (TotalTasks > 0)
            {
                OverallProgress = (double)nonGroupTasks.Sum(t => t.PercentComplete) / TotalTasks;
            }

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
            StatusSeries.Clear();
            var nonGroupTasks = _allTasks.Where(t => !t.IsGroup).ToList();
            
            // Group tasks by assignees. Since one task can have many assignees, 
            // we'll count occurrences of each staff member.
            var assigneeCounts = new Dictionary<string, int>();
            int unassigned = 0;

            foreach (var task in nonGroupTasks)
            {
                if (task.Assignments != null && task.Assignments.Any())
                {
                    foreach (var a in task.Assignments)
                    {
                        var name = a.AssigneeName ?? "Unknown";
                        assigneeCounts[name] = assigneeCounts.GetValueOrDefault(name) + 1;
                    }
                }
                else
                {
                    unassigned++;
                }
            }

            // Colors for workload distribution (shades of blue)
            var blueShades = new[] { "#1D4ED8", "#2563EB", "#3B82F6", "#60A5FA", "#93C5FD", "#BFDBFE" };
            int colorIndex = 0;

            foreach (var (name, count) in assigneeCounts.OrderByDescending(x => x.Value).Take(5))
            {
                AddStatusSeries(name, count, SKColor.Parse(blueShades[colorIndex % blueShades.Length]));
                colorIndex++;
            }

            if (assigneeCounts.Count > 5)
            {
                int otherCount = assigneeCounts.OrderByDescending(x => x.Value).Skip(5).Sum(x => x.Value);
                AddStatusSeries("Other Staff", otherCount, SKColor.Parse("#94A3B8")); // Greyish blue
            }

            if (unassigned > 0)
            {
                AddStatusSeries("Unassigned", unassigned, SKColor.Parse("#1E293B")); // Very dark blue/slate
            }

            ScheduleSeries.Clear();
            int behind = OverdueTasks;
            int delayed = DelayedStartTasks;
            int onTrack = Math.Max(0, TotalTasks - CompletedTasks - behind - delayed);

            // Using descriptive labels as requested: Ahead/Done, On Track, Delayed Start, Behind
            AddScheduleSeries("Ahead/Done", CompletedTasks, SKColor.Parse("#1D4ED8"));
            AddScheduleSeries("On Track", onTrack, SKColor.Parse("#60A5FA"));
            AddScheduleSeries("Delayed Start", delayed, SKColor.Parse("#38BDF8"));
            AddScheduleSeries("Behind", behind, SKColor.Parse("#1E3A8A"));

            ProgressGaugeSeries.Clear();
            ProgressGaugeSeries.Add(new PieSeries<double>
            {
                Values = new double[] { Math.Round(OverallProgress, 1) },
                Name = "Progress",
                InnerRadius = 35,
                MaxRadialColumnWidth = 10,
                Fill = new SolidColorPaint(SKColor.Parse(ProjectHealthColor))
            });
            
            // Add a subtle background ring for the gauge (remainder of 100%)
            ProgressGaugeSeries.Add(new PieSeries<double>
            {
                Values = new double[] { Math.Max(0, 100 - Math.Round(OverallProgress, 1)) },
                Name = "Background",
                InnerRadius = 35,
                MaxRadialColumnWidth = 10,
                Fill = new SolidColorPaint(new SKColor(255, 255, 255, 10)), // Very subtle white
                IsVisibleAtLegend = false
            });
        }

        private void AddStatusSeries(string name, double value, SKColor color)
        {
            if (value <= 0) return;
            StatusSeries.Add(new PieSeries<double>
            {
                Name = name,
                Values = new double[] { value },
                InnerRadius = 0,
                Fill = new SolidColorPaint(color),
                DataLabelsPosition = PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => $"{point.StackedValue?.Share ?? 0:P0}",
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 13
            });
        }

        private void AddScheduleSeries(string name, double value, SKColor color)
        {
            if (value <= 0) return;
            ScheduleSeries.Add(new PieSeries<double>
            {
                Name = name,
                Values = new double[] { value },
                InnerRadius = 0,
                Fill = new SolidColorPaint(color),
                DataLabelsPosition = PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => $"{point.StackedValue?.Share ?? 0:P0}",
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 13
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

            var startDate = _project.StartDate;
            var now = DateTime.Now;
            if (now <= startDate)
            {
                EtaDateString = _project.EndDate.ToString("dd MMM yyyy");
                EtaStatus = "Scheduled";
                return;
            }

            var timeElapsed = now - startDate;
            var totalEstimatedTimeTicks = timeElapsed.Ticks / (OverallProgress / 100.0);
            var predictedEndDate = startDate.AddTicks((long)totalEstimatedTimeTicks);
            EtaDateString = predictedEndDate.ToString("dd MMM yyyy");
            
            var varianceDays = (predictedEndDate - _project.EndDate).TotalDays;
            IsLate = varianceDays > 0;
            
            if (IsLate)
            {
                VarianceText = $"Deadline missed by {Math.Round(varianceDays)} days";
                EtaStatus = $"Expected {Math.Round(varianceDays)} days late";
            }
            else
            {
                VarianceText = string.Empty;
                EtaStatus = "On schedule";
            }
        }

        [RelayCommand]
        private void NavigateToTasks(string filter)
        {
            WeakReferenceMessenger.Default.Send(new ProjectDashboardNavigationMessage("Tasks", filter));
        }

        [RelayCommand]
        private void NavigateToSafety()
        {
            WeakReferenceMessenger.Default.Send(new ProjectDashboardNavigationMessage("HSEQ"));
        }

        [RelayCommand]
        private void NavigateToVariationOrders()
        {
            WeakReferenceMessenger.Default.Send(new ProjectDashboardNavigationMessage("VariationOrders"));
        }
    }
}
