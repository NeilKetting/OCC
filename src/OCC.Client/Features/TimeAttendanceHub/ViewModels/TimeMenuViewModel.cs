using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;
using OCC.Client.ViewModels.Core;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;

using OCC.Client.Infrastructure;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class TimeMenuViewModel : ViewModelBase, IRecipient<SwitchTabMessage>
    {
        #region Observables

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLiveActive))]
        [NotifyPropertyChangedFor(nameof(IsDailyActive))]
        [NotifyPropertyChangedFor(nameof(IsHistoryActive))]
        [NotifyPropertyChangedFor(nameof(IsLeaveActive))]
        [NotifyPropertyChangedFor(nameof(IsOvertimeActive))]
        [NotifyPropertyChangedFor(nameof(IsManualActive))]
        private string _activeTab = "Live";

        public bool IsClockSystemActive => ActiveTab is "Timesheet" or "History" or "Manual" or "Live V2" or "Timesheet V2";
        public bool IsLiveActive => ActiveTab is "Live" or "Live V2";
        public bool IsDailyActive => ActiveTab is "Timesheet" or "Timesheet V2";
        public bool IsHistoryActive => ActiveTab is "History";
        public bool IsManualActive => ActiveTab is "Manual";
        public bool IsLeaveActive => ActiveTab is "Leave Application" or "LeaveApprovals";
        public bool IsOvertimeActive => ActiveTab is "Overtime" or "OvertimeApproval";

        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
        }

        private readonly IPermissionService _permissionService;

        public bool CanApproveLeave => _permissionService.CanAccess(NavigationRoutes.Feature_LeaveApproval);
        public bool CanRequestOvertime => _permissionService.CanAccess(NavigationRoutes.Feature_OvertimeRequest);
        public bool CanApproveOvertime => _permissionService.CanAccess(NavigationRoutes.Feature_OvertimeApproval);
        public bool CanAccessCalendar => _permissionService.CanAccess(NavigationRoutes.Calendar);

        public TimeMenuViewModel(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public TimeMenuViewModel()
        {
             _permissionService = null!;
        }

        #endregion

        #region Methods

        public void Receive(SwitchTabMessage message)
        {
            // Optional: Only update if the message is relevant to Time views?
            // For now, simple like HomeMenuViewModel
            ActiveTab = message.Value;
        }

        #endregion
    }
}
