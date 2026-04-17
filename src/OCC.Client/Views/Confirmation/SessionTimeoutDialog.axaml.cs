using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace OCC.Client.Views.Confirmation
{
    public partial class SessionTimeoutDialog : Window
    {
        private DispatcherTimer _timer;
        private int _secondsRemaining = 60;

        public SessionTimeoutDialog()
        {
            InitializeComponent();
            
            // Start local countdown
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            UpdateDisplay();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _secondsRemaining--;
            UpdateDisplay();

            if (_secondsRemaining <= 0)
            {
                _timer.Stop();
                Close(false); // Valid logout
            }
        }

        private void UpdateDisplay()
        {
            var text = this.FindControl<TextBlock>("CountdownText");
            var prog = this.FindControl<ProgressBar>("TimeoutProgress");

            if (text != null) text.Text = $"{_secondsRemaining} seconds";
            if (prog != null) prog.Value = _secondsRemaining;
        }

        private void OnContinueClick(object? sender, RoutedEventArgs e)
        {
            _timer.Stop();
            Close(true); // User confirmed
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}
