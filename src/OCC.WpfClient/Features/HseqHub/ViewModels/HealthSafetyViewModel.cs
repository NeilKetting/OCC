using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
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
        private PerformanceMonitoringListViewModel _performanceView;

        [ObservableProperty]
        private IncidentListViewModel _incidentsView;

        [ObservableProperty]
        private TrainingListViewModel _trainingView;

        [ObservableProperty]
        private AuditListViewModel _auditsView;

        [ObservableProperty]
        private DocumentsListViewModel _documentsView;

        public HealthSafetyViewModel(
            HealthSafetyMenuViewModel menuViewModel,
            HealthSafetyDashboardViewModel dashboardView,
            PerformanceMonitoringListViewModel performanceView,
            IncidentListViewModel incidentsView,
            TrainingListViewModel trainingView,
            AuditListViewModel auditsView,
            DocumentsListViewModel documentsView)
        {
            MenuViewModel = menuViewModel;
            DashboardView = dashboardView;
            PerformanceView = performanceView;
            IncidentsView = incidentsView;
            TrainingView = trainingView;
            AuditsView = auditsView;
            DocumentsView = documentsView;
            Title = "HSEQ Hub";
            
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
                    AuditsView.LoadDataCommand.Execute(null);
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
