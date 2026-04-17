using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using System.Threading.Tasks;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class PerformanceMonitoringViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _healthSafetyService;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.HseqSafeHourRecord> _safeHours;

        [ObservableProperty]
        private bool _isLoading;

        public PerformanceMonitoringViewModel(IHealthSafetyService healthSafetyService)
        {
            _healthSafetyService = healthSafetyService;
            _safeHours = new System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.HseqSafeHourRecord>();
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var history = await _healthSafetyService.GetPerformanceHistoryAsync();
                SafeHours = new System.Collections.ObjectModel.ObservableCollection<OCC.Shared.Models.HseqSafeHourRecord>(history);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading H&S performance: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
