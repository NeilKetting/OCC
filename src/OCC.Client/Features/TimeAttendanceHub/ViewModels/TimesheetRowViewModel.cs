using CommunityToolkit.Mvvm.ComponentModel;
using System;

using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class TimesheetRowViewModel : ViewModelBase
    {
        #region Observables

        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _taskName = string.Empty;

        [ObservableProperty]
        private double? _mondayHours;

        [ObservableProperty]
        private double? _tuesdayHours;

        [ObservableProperty]
        private double? _wednesdayHours;

        [ObservableProperty]
        private double? _thursdayHours;

        [ObservableProperty]
        private double? _fridayHours;

        [ObservableProperty]
        private double? _saturdayHours;

        [ObservableProperty]
        private double? _sundayHours;

        #endregion

        #region Properties

        public double TotalHours => (MondayHours ?? 0) + (TuesdayHours ?? 0) + (WednesdayHours ?? 0) +
                                    (ThursdayHours ?? 0) + (FridayHours ?? 0) + (SaturdayHours ?? 0) +
                                    (SundayHours ?? 0);

        public double CompletionPercentage => 0; // Placeholder for now

        #endregion

        #region Methods

        partial void OnMondayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));
        partial void OnTuesdayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));
        partial void OnWednesdayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));
        partial void OnThursdayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));
        partial void OnFridayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));
        partial void OnSaturdayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));
        partial void OnSundayHoursChanged(double? value) => OnPropertyChanged(nameof(TotalHours));

        #endregion
    }
}
