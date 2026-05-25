using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// Housing ViewModel for the Time & Attendance Hub.
    /// Mirrors the HSEQ HealthSafetyViewModel pattern.
    /// </summary>
    public partial class AttendanceViewModel : ViewModelBase
    {
        [ObservableProperty] private AttendanceMenuViewModel _menuViewModel;
        [ObservableProperty] private ViewModelBase _currentView;

        [ObservableProperty] private AttendanceDashboardViewModel _dashboardView;
        [ObservableProperty] private AttendanceHistoryListViewModel _historyView;
        [ObservableProperty] private TeamManagementViewModel _teamsView;

        public AttendanceViewModel(
            AttendanceMenuViewModel menuViewModel,
            AttendanceDashboardViewModel dashboardView,
            AttendanceHistoryListViewModel historyView,
            TeamManagementViewModel teamsView)
        {
            MenuViewModel = menuViewModel;
            DashboardView = dashboardView;
            HistoryView = historyView;
            TeamsView = teamsView;
            Title = "Time & Attendance";

            CurrentView = DashboardView;
            DashboardView.LoadDashboardDataCommand.Execute(null);

            MenuViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AttendanceMenuViewModel.ActiveTab))
                    SwitchView();
            };
        }

        private void SwitchView()
        {
            switch (MenuViewModel.ActiveTab)
            {
                case "History":
                    CurrentView = HistoryView;
                    HistoryView.LoadDataCommand.Execute(null);
                    break;
                case "Teams":
                    CurrentView = TeamsView;
                    TeamsView.LoadDataCommand.Execute(null);
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
