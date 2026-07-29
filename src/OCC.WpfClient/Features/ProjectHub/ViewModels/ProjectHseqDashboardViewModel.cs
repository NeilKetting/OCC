using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Infrastructure;
using System.Collections.Generic;
using OCC.WpfClient.Services.Interfaces;
using System.Threading.Tasks;
using System.Linq;
using System;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.DTOs;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectHseqDashboardViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;

        [ObservableProperty] private Guid _projectId;
        [ObservableProperty] private string _projectName = string.Empty;

        // KPI Stats
        [ObservableProperty] private double _totalSafeHours;
        [ObservableProperty] private int _incidentsTotal;
        [ObservableProperty] private int _auditsTotal;
        [ObservableProperty] private decimal _averageAuditScore;

        // Pie distribution count
        [ObservableProperty] private int _nearMisses;
        [ObservableProperty] private int _injuries;
        [ObservableProperty] private int _environmentals;

        // 1. Audit Score Trend Line Chart
        public ObservableCollection<ISeries> AuditTrendSeries { get; set; } = new();
        public ObservableCollection<Axis> AuditTrendXAxes { get; set; } = new();
        public ObservableCollection<Axis> AuditTrendYAxes { get; set; } = new();

        // 2. HSEQ Category Score Row/Bar Chart
        public ObservableCollection<ISeries> CategoryBreakdownSeries { get; set; } = new();
        public ObservableCollection<Axis> CategoryXAxes { get; set; } = new();
        public ObservableCollection<Axis> CategoryYAxes { get; set; } = new();

        // 3. Incident Distribution Pie Chart
        public ObservableCollection<ISeries> IncidentDistributionSeries { get; set; } = new();

        public SolidColorPaint LegendTextPaint { get; } = new SolidColorPaint(SKColors.LightGray);

        public ProjectHseqDashboardViewModel(IHealthSafetyService hseqService)
        {
            _hseqService = hseqService;
            Title = "HSEQ Dashboard";

            // Setup Trend Axes
            AuditTrendXAxes.Add(new Axis 
            { 
                LabelsRotation = 0, 
                Labels = new List<string>(),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray.WithAlpha(30)),
                MinStep = 1,
                ForceStepToMin = true
            });
            AuditTrendYAxes.Add(new Axis 
            { 
                MinLimit = 0, 
                MaxLimit = 100, 
                Labeler = v => $"{v}%",
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray.WithAlpha(30)),
                MinStep = 20,
                ForceStepToMin = true
            });

            CategoryXAxes.Add(new Axis 
            { 
                LabelsRotation = 90, 
                Labels = new List<string>(),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray.WithAlpha(30)),
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 10
            });
            CategoryYAxes.Add(new Axis 
            { 
                MinLimit = 0, 
                MaxLimit = 100, 
                Labeler = v => $"{v}%",
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray.WithAlpha(30)),
                MinStep = 20,
                ForceStepToMin = true
            });
        }

        // Design-time fallback
        public ProjectHseqDashboardViewModel()
        {
            _hseqService = null!;
        }

        public void Initialize(Guid projectId, string projectName)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            if (_hseqService == null || ProjectId == Guid.Empty) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading HSEQ dashboard...";

                var stats = await _hseqService.GetProjectDashboardStatsAsync(ProjectId);
                if (stats != null)
                {
                    TotalSafeHours = stats.TotalSafeHours;
                    IncidentsTotal = stats.IncidentsTotal;
                    AuditsTotal = stats.AuditsTotal;
                    AverageAuditScore = stats.AverageAuditScore;
                    NearMisses = stats.NearMisses;
                    Injuries = stats.Injuries;
                    Environmentals = stats.Environmentals;

                    UpdateTrendChart(stats.RecentAuditScores);
                    UpdateCategoryChart(stats.CategoryStats);
                    UpdateIncidentPieChart();
                }
            }
            catch
            {
                // Silent fallback
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateTrendChart(List<ProjectAuditScoreDto> scores)
        {
            AuditTrendSeries.Clear();
            AuditTrendXAxes[0].Labels = new List<string>();

            if (scores == null || !scores.Any()) return;

            var dates = scores.Select(s => s.Date).ToList();
            var values = scores.Select(s => (double)s.ActualScore).ToList();

            AuditTrendXAxes[0].Labels = dates;
            AuditTrendSeries.Add(new ColumnSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(SKColors.Cyan.WithAlpha(160)),
                Stroke = new SolidColorPaint(SKColors.Cyan, 1),
                MaxBarWidth = 40,
                DataLabelsSize = 12,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                DataLabelsFormatter = p => $"{p.Coordinate.PrimaryValue}%",
                Name = "Audit Score"
            });
        }

        private void UpdateCategoryChart(List<ProjectCategoryStatDto> catStats)
        {
            CategoryBreakdownSeries.Clear();
            CategoryXAxes[0].Labels = new List<string>();

            if (catStats == null || !catStats.Any()) return;

            var names = catStats.Select(c => c.CategoryName).ToList();
            var values = catStats.Select(c => (double)c.AveragePercentage).ToList();

            CategoryXAxes[0].Labels = names;
            CategoryBreakdownSeries.Add(new ColumnSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(SKColors.CornflowerBlue.WithAlpha(160)),
                Stroke = new SolidColorPaint(SKColors.CornflowerBlue, 1),
                Name = "Average Score"
            });
        }

        private void UpdateIncidentPieChart()
        {
            IncidentDistributionSeries.Clear();

            if (IncidentsTotal == 0)
            {
                IncidentDistributionSeries.Add(new PieSeries<int>
                {
                    Values = new[] { 1 },
                    Name = "No Incidents",
                    Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(50))
                });
                return;
            }

            if (NearMisses > 0)
            {
                IncidentDistributionSeries.Add(new PieSeries<int>
                {
                    Values = new[] { NearMisses },
                    Name = "Near Misses",
                    Fill = new SolidColorPaint(SKColors.Yellow.WithAlpha(180))
                });
            }

            if (Injuries > 0)
            {
                IncidentDistributionSeries.Add(new PieSeries<int>
                {
                    Values = new[] { Injuries },
                    Name = "Injuries",
                    Fill = new SolidColorPaint(SKColors.Tomato.WithAlpha(180))
                });
            }

            if (Environmentals > 0)
            {
                IncidentDistributionSeries.Add(new PieSeries<int>
                {
                    Values = new[] { Environmentals },
                    Name = "Environmental",
                    Fill = new SolidColorPaint(SKColors.SeaGreen.WithAlpha(180))
                });
            }
        }
    }
}
