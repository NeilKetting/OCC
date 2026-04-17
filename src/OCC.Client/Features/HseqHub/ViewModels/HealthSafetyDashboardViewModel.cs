using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using System.Collections.Generic;
using OCC.Client.Services.Interfaces;
using System.Threading.Tasks;
using System.Linq;
using System;
using CommunityToolkit.Mvvm.Input;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class HealthSafetyDashboardViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;

        // 1. Audit Scores
        public ObservableCollection<ISeries> AuditScoreSeries { get; set; } = new();
        public ObservableCollection<Axis> AuditXAxes { get; set; } = new();
        public ObservableCollection<Axis> AuditYAxes { get; set; } = new();

        // 2. Incident Statistics
        public ObservableCollection<ISeries> IncidentSeries { get; set; } = new();
        public ObservableCollection<Axis> IncidentXAxes { get; set; } = new();
        public ObservableCollection<Axis> IncidentYAxes { get; set; } = new();

        // 3. Stats Cards
        [ObservableProperty]
        private double _totalSafeHours;

        [ObservableProperty]
        private int _totalIncidents;
        
        [ObservableProperty]
        private int _openAudits;

        public HealthSafetyDashboardViewModel(IHealthSafetyService hseqService, IToastService toastService)
        {
            _hseqService = hseqService;
            _toastService = toastService;
            
            // Initial Axis Setup
            AuditXAxes.Add(new Axis { LabelsRotation = 0, Labels = new List<string>() });
            AuditYAxes.Add(new Axis { MinLimit = 0, MaxLimit = 100, Labeler = v => $"{v}%" });

            IncidentXAxes.Add(new Axis { Labels = new[] { "Incidents", "Near Misses", "Injuries" } });
            IncidentYAxes.Add(new Axis { MinLimit = 0, MinStep = 1 });
        }

        // Design-time
        public HealthSafetyDashboardViewModel()
        {
             _hseqService = null!;
             _toastService = null!;
        }

        [RelayCommand]
        public async Task LoadDashboardData()
        {
            if (_hseqService == null) return;

            try
            {
               var stats = await _hseqService.GetDashboardStatsAsync();
               if (stats != null)
               {
                   TotalSafeHours = stats.TotalSafeHours;
                   TotalIncidents = stats.IncidentsTotal;
                   
                   // Update Charts
                   UpdateAuditChart(stats.RecentAuditScores);
                   UpdateIncidentChart(stats);
               }
            }
            catch (Exception)
            {
                // Silent fail or log
            }
        }

        private void UpdateAuditChart(List<AuditScoreDto> scores)
        {
            if (scores == null || !scores.Any()) return;

            var labels = scores.Select(s => s.SiteName).ToList();
            var values = scores.Select(s => (double)s.ActualScore).ToList();

            AuditScoreSeries.Clear();
            AuditScoreSeries.Add(new ColumnSeries<double>
            {
                Name = "Score",
                Values = values,
                Fill = new SolidColorPaint(SKColors.Teal),
                MaxBarWidth = 40,
                DataLabelsSize = 12,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                DataLabelsFormatter = p => $"{p.Coordinate.PrimaryValue}%"
            });
            
            AuditXAxes[0].Labels = labels;
        }

        private void UpdateIncidentChart(HseqDashboardStats stats)
        {
            IncidentSeries.Clear();
            IncidentSeries.Add(new ColumnSeries<int>
            {
                 Name = "Count",
                 Values = new ObservableCollection<int> { stats.IncidentsTotal, stats.NearMisses, stats.Injuries },
                 Fill = new SolidColorPaint(SKColors.Orange),
                 MaxBarWidth = 40
            });
        }
    }
}
