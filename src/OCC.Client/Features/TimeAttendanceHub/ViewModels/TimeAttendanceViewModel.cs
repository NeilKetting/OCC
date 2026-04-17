using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using Microsoft.Extensions.Logging;

using OCC.Client.Infrastructure;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class TimeAttendanceViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IAuthService _authService;

        #endregion

        #region Observables

        [ObservableProperty]
        private TimeMenuViewModel _timeMenu;

        [ObservableProperty]
        private ViewModelBase _currentView;

        // Sub-ViewModels
        [ObservableProperty] private TimeLiveViewModel _liveView;
        [ObservableProperty] private TimeLiveV2ViewModel _liveV2View;
        [ObservableProperty] private DailyTimesheetViewModel _dailyTimesheetView;
        [ObservableProperty] private DailyTimesheetV2ViewModel _dailyTimesheetV2View;
        [ObservableProperty] private AttendanceHistoryViewModel _attendanceHistoryView;
        [ObservableProperty] private LeaveApplicationViewModel _leaveApplicationView;
        [ObservableProperty] private LeaveApprovalViewModel _leaveApprovalVM;
        [ObservableProperty] private OvertimeViewModel _overtimeVM;
        [ObservableProperty] private OvertimeApprovalViewModel _overtimeApprovalVM;
        [ObservableProperty] private ManualAttendanceViewModel _manualAttendanceView;

        [ObservableProperty]
        private string _greeting = string.Empty;

        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dd MMMM yyyy");

        #endregion

        #region Constructors

        public TimeAttendanceViewModel()
        {
            // Design-time
            _timeMenu = null!;
            _liveView = null!;
            _liveV2View = null!;
            _dailyTimesheetView = null!;
            _dailyTimesheetV2View = null!;
            _attendanceHistoryView = null!;
            _leaveApplicationView = null!;
            _leaveApprovalVM = null!;
            _overtimeVM = null!;
            _overtimeApprovalVM = null!;
            _manualAttendanceView = null!;
            _currentView = null!;

            _authService = null!;
        }

        public TimeAttendanceViewModel(
            TimeMenuViewModel timeMenu,
            TimeLiveViewModel liveView,
            TimeLiveV2ViewModel liveV2View,
            DailyTimesheetViewModel dailyTimesheetView,
            DailyTimesheetV2ViewModel dailyTimesheetV2View,
            AttendanceHistoryViewModel attendanceHistoryView,
            LeaveApplicationViewModel leaveApplicationView,
            LeaveApprovalViewModel leaveApprovalViewModel,
            OvertimeViewModel overtimeViewModel, 
            OvertimeApprovalViewModel overtimeApprovalViewModel, 
            ManualAttendanceViewModel manualAttendanceViewModel,
            IAuthService authService)
        {
            _timeMenu = timeMenu;
            _liveView = liveView;
            _liveV2View = liveV2View;
            _dailyTimesheetView = dailyTimesheetView;
            _dailyTimesheetV2View = dailyTimesheetV2View;
            _attendanceHistoryView = attendanceHistoryView;
            _leaveApplicationView = leaveApplicationView;
            _leaveApprovalVM = leaveApprovalViewModel;
            _overtimeVM = overtimeViewModel;
            _overtimeApprovalVM = overtimeApprovalViewModel;
            _manualAttendanceView = manualAttendanceViewModel;
            _authService = authService;
            
            // Default View
            _currentView = _liveView;

            // Ensure Tab selection matches default view
            _timeMenu.ActiveTab = "Live";

            _timeMenu.PropertyChanged += TimeMenu_PropertyChanged;
            
            Initialize();
        }

        #endregion

        #region Methods

        private void Initialize()
        {
            var now = DateTime.Now;
            Greeting = GetGreeting(now);
            CurrentDate = now.ToString("dd MMMM yyyy");
            UpdateVisibility();
        }

        private void TimeMenu_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimeMenuViewModel.ActiveTab))
            {
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            switch (TimeMenu.ActiveTab)
            {
                case "Timesheet":
                    CurrentView = DailyTimesheetView;
                    break;
                case "Timesheet V2":
                    CurrentView = DailyTimesheetV2View;
                    _ = DailyTimesheetV2View.LoadDataAsync();
                    break;
                case "Live V2":
                    CurrentView = LiveV2View;
                    _ = LiveV2View.LoadDataAsync();
                    break;
                case "History":
                    CurrentView = AttendanceHistoryView;
                    break;
                case "Leave Application":
                    CurrentView = LeaveApplicationView;
                    break;
                case NavigationRoutes.Feature_LeaveApproval:
                    CurrentView = LeaveApprovalVM;
                    break;

                case "Manual":
                    CurrentView = ManualAttendanceView;
                    _ = ManualAttendanceView.LoadEmployeesCommand.ExecuteAsync(null);
                    break;
                case "Overtime":
                    CurrentView = OvertimeVM;
                    break;
                case NavigationRoutes.Feature_OvertimeApproval:
                    CurrentView = OvertimeApprovalVM;
                    break;
                case NavigationRoutes.Home:
                case "Live":
                default:
                    CurrentView = LiveView;
                    break;
            }
        }

        private string GetGreeting(DateTime time)
        {
             string timeGreeting = time.Hour < 12 ? "Good morning" :
                                  time.Hour < 18 ? "Good afternoon" : "Good evening";

            var userName = _authService.CurrentUser?.DisplayName ?? "User";
            return $"{timeGreeting}, {userName}";
        }

        #endregion
    }
}
