using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class HealthSafetyViewModel : ViewModelBase
    {
        [ObservableProperty]
        private HealthSafetyMenuViewModel _menuViewModel;

        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private HealthSafetyDashboardViewModel _dashboardView;

        [ObservableProperty]
        private PerformanceMonitoringViewModel _performanceView;

        [ObservableProperty]
        private IncidentsViewModel _incidentsView;

        [ObservableProperty]
        private TrainingViewModel _trainingView;

        [ObservableProperty]
        private AuditsViewModel _auditsView;

        [ObservableProperty]
        private DocumentsViewModel _documentsView;

        public HealthSafetyViewModel()
        {
            // Design-time
            _menuViewModel = null!;
            _dashboardView = null!;
            _performanceView = null!;
            _incidentsView = null!;
            _trainingView = null!;
            _auditsView = null!;
            _documentsView = null!;
            _currentView = null!;
        }

        public HealthSafetyViewModel(
            HealthSafetyMenuViewModel menuViewModel,
            HealthSafetyDashboardViewModel dashboardView,
            PerformanceMonitoringViewModel performanceView,
            IncidentsViewModel incidentsView,
            TrainingViewModel trainingView,
            AuditsViewModel auditsView,
            DocumentsViewModel documentsView)
        {
            MenuViewModel = menuViewModel;
            DashboardView = dashboardView;
            PerformanceView = performanceView;
            IncidentsView = incidentsView;
            TrainingView = trainingView;
            AuditsView = auditsView;
            DocumentsView = documentsView;
            
            // Default view
            CurrentView = DashboardView;
            DashboardView.LoadDashboardDataCommand.Execute(null);

            MenuViewModel.PropertyChanged += MenuViewModel_PropertyChanged;
        }

        private void MenuViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HealthSafetyMenuViewModel.ActiveTab))
            {
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            switch (MenuViewModel.ActiveTab)
            {
                case "Performance Monitoring":
                    CurrentView = PerformanceView;
                    PerformanceView.LoadDataCommand.Execute(null);
                    break;
                case "Incidents":
                    CurrentView = IncidentsView;
                    break;
                case "Training":
                    CurrentView = TrainingView;
                    break;
                case "Audits":
                    CurrentView = AuditsView;
                    AuditsView.LoadAuditsCommand.Execute(null);
                    break;
                case "Documents":
                    CurrentView = DocumentsView;
                    break;
                case "Dashboard":
                default:
                    CurrentView = DashboardView;
                    DashboardView.LoadDashboardDataCommand.Execute(null);
                    break;
            }
        }
    }
}
