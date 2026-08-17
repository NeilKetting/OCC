using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Dialogs
{
    /// <summary>
    /// Interaction logic for AttendanceOverrideDialogView.xaml.
    /// Provides direct line-level overrides for individual attendance records with live preview and mandatory audit reason logging.
    /// </summary>
    public partial class AttendanceOverrideDialogView : Window
    {
        private readonly AttendanceRecord _record;
        private readonly string _employeeName;
        private readonly string _branch;
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly AttendanceStatus _previousStatus;
        private bool _isInitializing = true;

        public AttendanceOverrideDialogView(
            AttendanceRecord record,
            string employeeName,
            string branch,
            IAttendanceService attendanceService,
            IEmployeeService employeeService)
        {
            InitializeComponent();
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _employeeName = employeeName ?? "Unknown";
            _branch = branch ?? "General";
            _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
            _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
            _previousStatus = _record.Status;

            PopulateInitialValues();
            _isInitializing = false;
            RecalculateLivePreview();
        }

        private void PopulateInitialValues()
        {
            EmpNameText.Text = _employeeName;
            EmpNumberText.Text = _record.EmployeeId.HasValue ? _record.EmployeeId.Value.ToString().Substring(0, 8).ToUpper() : "RECORD";
            RecordDateText.Text = _record.Date.ToString("yyyy/MM/dd");
            EmpBranchText.Text = _branch;
            CurrentHoursText.Text = $"{_record.HoursWorked:F2} hrs";
            OriginalHoursText.Text = _record.HoursWorked.ToString("F2", CultureInfo.InvariantCulture);

            // Select Status ComboBox
            SetStatusComboBoxItem(_record.Status);

            // Project / Custom Site
            ProjectSiteInput.Text = _record.CustomSite ?? string.Empty;

            // Clock In / Out Times
            ClockInInput.Text = _record.CheckInTime.HasValue ? _record.CheckInTime.Value.ToString("HH:mm") : string.Empty;
            ClockOutInput.Text = _record.CheckOutTime.HasValue ? _record.CheckOutTime.Value.ToString("HH:mm") : string.Empty;

            // Worked & Paid Leave Hours
            HoursWorkedInput.Text = _record.HoursWorked.ToString("F2", CultureInfo.InvariantCulture);
            PaidLeaveHoursInput.Text = _record.PaidLeaveHours.HasValue ? _record.PaidLeaveHours.Value.ToString("F2", CultureInfo.InvariantCulture) : "0.00";
        }

        private void SetStatusComboBoxItem(AttendanceStatus status)
        {
            foreach (ComboBoxItem item in StatusInput.Items)
            {
                if (item.Content is string s && Enum.TryParse<AttendanceStatus>(s.Replace(" ", ""), true, out var parsedStatus) && parsedStatus == status)
                {
                    StatusInput.SelectedItem = item;
                    return;
                }
            }
            if (StatusInput.Items.Count > 0) StatusInput.SelectedIndex = 0;
        }

        private AttendanceStatus GetSelectedStatus()
        {
            if (StatusInput.SelectedItem is ComboBoxItem item && item.Content is string s)
            {
                string cleanStatusStr = s.Replace(" ", "");
                if (Enum.TryParse<AttendanceStatus>(cleanStatusStr, true, out var parsed))
                {
                    return parsed;
                }
            }
            return _record.Status;
        }

        private void FieldValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            RecalculateLivePreview();
        }

        private void FieldValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            RecalculateLivePreview();
        }

        private void RecalculateLivePreview()
        {
            double origHours = _record.HoursWorked;
            double newHours = ParseDouble(HoursWorkedInput.Text);

            NewHoursPreviewText.Text = $"{newHours:F2} hrs";

            double diff = newHours - origHours;
            if (Math.Abs(diff) < 0.001)
            {
                HoursDiffText.Text = " (No Change)";
                HoursDiffText.Foreground = (System.Windows.Media.Brush?)TryFindResource("TextSub")
                    ?? System.Windows.Media.Brushes.Gray;
            }
            else if (diff > 0)
            {
                HoursDiffText.Text = $" (+{diff:F2}h)";
                HoursDiffText.Foreground = (System.Windows.Media.Brush?)TryFindResource("SuccessGreen") 
                    ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#34D399")!;
            }
            else
            {
                HoursDiffText.Text = $" ({diff:F2}h)";
                HoursDiffText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private async void ApplyOverride_Click(object sender, RoutedEventArgs e)
        {
            var reason = ReasonInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                ValidationErrorText.Visibility = Visibility.Visible;
                ReasonInput.Focus();
                return;
            }

            ValidationErrorText.Visibility = Visibility.Collapsed;
            var changes = new List<string>();

            var newStatus = GetSelectedStatus();
            if (_record.Status != newStatus)
            {
                changes.Add($"Status {_record.Status} ➔ {newStatus}");
                _record.Status = newStatus;
            }

            double newHours = ParseDouble(HoursWorkedInput.Text);
            if (Math.Abs(_record.HoursWorked - newHours) > 0.001)
            {
                changes.Add($"Hours {_record.HoursWorked:F2} ➔ {newHours:F2}");
                _record.HoursWorked = newHours;
            }

            double newPaidLeave = ParseDouble(PaidLeaveHoursInput.Text);
            double origPaidLeave = _record.PaidLeaveHours ?? 0;
            if (Math.Abs(origPaidLeave - newPaidLeave) > 0.001)
            {
                changes.Add($"Paid Leave {origPaidLeave:F2} ➔ {newPaidLeave:F2}");
                _record.PaidLeaveHours = newPaidLeave;
            }

            // Check-in / out times
            if (TimeSpan.TryParse(ClockInInput.Text?.Trim(), out var inTime))
            {
                var newCheckIn = _record.Date.Date.Add(inTime);
                if (_record.CheckInTime != newCheckIn)
                {
                    changes.Add($"ClockIn {_record.CheckInTime:HH:mm} ➔ {newCheckIn:HH:mm}");
                    _record.CheckInTime = newCheckIn;
                }
            }

            if (TimeSpan.TryParse(ClockOutInput.Text?.Trim(), out var outTime))
            {
                var newCheckOut = _record.Date.Date.Add(outTime);
                if (_record.CheckOutTime != newCheckOut)
                {
                    changes.Add($"ClockOut {_record.CheckOutTime:HH:mm} ➔ {newCheckOut:HH:mm}");
                    _record.CheckOutTime = newCheckOut;
                }
            }

            var customSite = ProjectSiteInput.Text?.Trim();
            if (_record.CustomSite != customSite)
            {
                changes.Add($"Site '{_record.CustomSite}' ➔ '{customSite}'");
                _record.CustomSite = customSite;
            }

            // Append audit notes to record Notes
            string changeSummary = changes.Count > 0 ? string.Join(", ", changes) : "Manual Override";
            string auditNote = $"[Override: {changeSummary}. Reason: {reason}]";

            if (string.IsNullOrWhiteSpace(_record.Notes))
            {
                _record.Notes = auditNote;
            }
            else
            {
                _record.Notes = (_record.Notes.Trim() + "; " + auditNote).Trim();
            }

            try
            {
                // Save attendance record update
                await _attendanceService.UpdateAttendanceRecordAsync(_record);

                // Adjust sick leave balance if status changed
                if (_record.EmployeeId.HasValue)
                {
                    if (_previousStatus != AttendanceStatus.Sick && _record.Status == AttendanceStatus.Sick)
                    {
                        var emp = await _employeeService.GetEmployeeAsync(_record.EmployeeId.Value);
                        if (emp != null)
                        {
                            emp.SickLeaveBalance = Math.Max(0, emp.SickLeaveBalance - 1);
                            await UpdateEmployeeBalanceAsync(emp);
                        }
                    }
                    else if (_previousStatus == AttendanceStatus.Sick && _record.Status != AttendanceStatus.Sick)
                    {
                        var emp = await _employeeService.GetEmployeeAsync(_record.EmployeeId.Value);
                        if (emp != null)
                        {
                            emp.SickLeaveBalance += 1;
                            await UpdateEmployeeBalanceAsync(emp);
                        }
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving attendance override: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateEmployeeBalanceAsync(OCC.Shared.DTOs.EmployeeDto emp)
        {
            var dto = await _employeeService.GetEmployeeAsync(emp.Id);
            if (dto != null)
            {
                dto.SickLeaveBalance = emp.SickLeaveBalance;
                dto.AnnualLeaveBalance = emp.AnnualLeaveBalance;
                var fullEmp = new OCC.WpfClient.Features.EmployeeHub.Models.EmployeeModel(dto).ToEntity();
                await _employeeService.UpdateEmployeeAsync(fullEmp);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private double ParseDouble(string? text)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                return val;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
                return val;
            return 0.0;
        }
    }
}
