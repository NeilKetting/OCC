using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using OCC.Shared.Models;
using System;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class PerformanceMonitoringViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _healthSafetyService;

        [ObservableProperty]
        private ObservableCollection<HseqSafeHourRecord> _safeHours = new();

        public PerformanceMonitoringViewModel(IHealthSafetyService healthSafetyService)
        {
            _healthSafetyService = healthSafetyService;
            Title = "Performance";
            _ = LoadDataAsync();
        }

        // Design-time
        public PerformanceMonitoringViewModel()
        {
            _healthSafetyService = null!;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        public async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var history = await _healthSafetyService.GetPerformanceHistoryAsync();
                if (history != null)
                {
                    SafeHours = new ObservableCollection<HseqSafeHourRecord>(history);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading H&S performance: {ex.Message}");
                NotifyError("Error", "Failed to load performance data.");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
