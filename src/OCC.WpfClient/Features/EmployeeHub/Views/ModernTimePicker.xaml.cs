using System;
using System.Windows;
using System.Windows.Controls;

namespace OCC.WpfClient.Features.EmployeeHub.Views
{
    public partial class ModernTimePicker : UserControl
    {
        public static readonly DependencyProperty SelectedTimeProperty =
            DependencyProperty.Register("SelectedTime", typeof(TimeSpan?), typeof(ModernTimePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTimeChanged));

        public TimeSpan? SelectedTime
        {
            get => (TimeSpan?)GetValue(SelectedTimeProperty);
            set => SetValue(SelectedTimeProperty, value);
        }

        private bool _isUpdating;

        public ModernTimePicker()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateSelectors();
        }

        private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernTimePicker picker)
            {
                picker.UpdateSelectors();
            }
        }

        private void UpdateSelectors()
        {
            if (_isUpdating || !IsLoaded) return;
            _isUpdating = true;

            try
            {
                if (SelectedTime.HasValue)
                {
                    HourBox.SelectedValue = SelectedTime.Value.Hours.ToString("D2");
                    // Round minutes to nearest 15 for the selector
                    int mins = (int)(Math.Round(SelectedTime.Value.Minutes / 15.0) * 15);
                    if (mins == 60) mins = 45; // Clamp
                    MinuteBox.SelectedValue = mins.ToString("D2");
                }
                else
                {
                    HourBox.SelectedIndex = -1;
                    MinuteBox.SelectedIndex = -1;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void TimePartChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || !IsLoaded) return;
            _isUpdating = true;

            try
            {
                string? hStr = HourBox.SelectedValue as string;
                string? mStr = MinuteBox.SelectedValue as string;

                if (hStr != null && mStr != null)
                {
                    if (int.TryParse(hStr, out int h) && int.TryParse(mStr, out int m))
                    {
                        SelectedTime = new TimeSpan(h, m, 0);
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
