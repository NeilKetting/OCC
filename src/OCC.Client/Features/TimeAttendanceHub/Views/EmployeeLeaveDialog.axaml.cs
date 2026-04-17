using Avalonia.Controls;
using Avalonia.Interactivity;
using OCC.Shared.Models;
using System;

namespace OCC.Client.Features.TimeAttendanceHub.Views
{
    public partial class EmployeeLeaveDialog : Window
    {
        private LeaveRequest? _request;
        public Action<LeaveRequest?>? CloseAction { get; set; }

        public EmployeeLeaveDialog()
        {
            InitializeComponent();
        }

        public EmployeeLeaveDialog(LeaveRequest request)
        {
            InitializeComponent();
            _request = request;

            // Initialize ComboBox items
            LeaveTypeComboBox.ItemsSource = Enum.GetValues(typeof(LeaveType));

            // Load data into fields
            if (_request != null)
            {
                StartDatePicker.SelectedDate = _request.StartDate;
                EndDatePicker.SelectedDate = _request.EndDate;
                LeaveTypeComboBox.SelectedItem = _request.LeaveType;
                ReasonTextBox.Text = _request.Reason;
            }
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (_request == null) return;
            
            if (StartDatePicker.SelectedDate.HasValue) _request.StartDate = StartDatePicker.SelectedDate.Value.Date;
            if (EndDatePicker.SelectedDate.HasValue) _request.EndDate = EndDatePicker.SelectedDate.Value.Date;
            
            if (LeaveTypeComboBox.SelectedItem is LeaveType type)
            {
                _request.LeaveType = type;
            }

            _request.Reason = ReasonTextBox.Text ?? string.Empty;

            Close(_request);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
