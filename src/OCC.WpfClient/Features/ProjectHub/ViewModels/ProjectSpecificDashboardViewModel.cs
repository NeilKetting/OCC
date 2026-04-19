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

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectSpecificDashboardViewModel : ViewModelBase
    {
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
        
        public SolidColorPaint LegendTextPaint { get; } = new SolidColorPaint(SKColors.White);

        public ObservableCollection<ISeries> StatusSeries { get; set; } = new();
        public ObservableCollection<ISeries> ScheduleSeries { get; set; } = new();
        public ObservableCollection<ISeries> ProgressGaugeSeries { get; set; } = new();

        public ProjectSpecificDashboardViewModel()
        {
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
                }
            });
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
            InProgressTasks = nonGroupTasks.Count(t => t.Status == "In Progress" || t.Status == "Started");
            ToDoTasks = nonGroupTasks.Count(t => t.Status == "To Do" || t.Status == "New" || t.Status == "Not Started");

            var now = DateTime.Now;
            OverdueTasks = nonGroupTasks.Count(t => !t.IsComplete && t.FinishDate < now);
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
            EtaStatus = varianceDays > 7 ? $"Expected {Math.Round(varianceDays)} days late" : "On schedule";
        }
    }
}
