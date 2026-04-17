using System;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services.Infrastructure
{
    public partial class UserActivityService : ObservableObject, IDisposable
    {
        private readonly DispatcherTimer _idleTimer;
        private DateTime _lastActivity = DateTime.Now;
        private const int IdleThresholdMinutes = 1; // "Away" status

        public double LogoutThresholdMinutes { get; set; } = 5.0;
        public double WarningThresholdMinutes => Math.Max(0.5, LogoutThresholdMinutes - 1.0);

        [ObservableProperty]
        private bool _isAway;

        [ObservableProperty]
        private string _statusText = "Active";

        private readonly ISignalRService _signalRService;
        private readonly LocalSettingsService _localSettingsService;

        public event EventHandler? SessionWarning;
        public event EventHandler? SessionExpired;
        public event EventHandler? SessionResumed;

        private bool _warningShown;

        public UserActivityService(ISignalRService signalRService, LocalSettingsService localSettingsService)
        {
            _signalRService = signalRService;
            _localSettingsService = localSettingsService;
            
            LogoutThresholdMinutes = _localSettingsService.Settings.SessionTimeoutMinutes;

            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _idleTimer.Tick += CheckIdleStatus;
            _idleTimer.Start();

            // Monitor global input in WPF
            InputManager.Current.PreProcessInput += OnInputActivity;
        }

        private void CheckIdleStatus(object? sender, EventArgs e)
        {
            var idleTime = DateTime.Now - _lastActivity;
            
            // 1. Session Timeout Logic
            if (idleTime.TotalMinutes >= LogoutThresholdMinutes)
            {
                SessionExpired?.Invoke(this, EventArgs.Empty);
                _idleTimer.Stop();
                return;
            }

            if (idleTime.TotalMinutes >= WarningThresholdMinutes && !_warningShown)
            {
                _warningShown = true;
                SessionWarning?.Invoke(this, EventArgs.Empty);
            }

            // 2. Away Logic
            if (idleTime.TotalMinutes >= IdleThresholdMinutes && !IsAway)
            {
                IsAway = true;
                UpdateAwayScale(idleTime);
                _ = _signalRService.UpdateStatusAsync("Away");
            }
            else if (IsAway)
            {
                UpdateAwayScale(idleTime);
            }
        }

        private void UpdateAwayScale(TimeSpan idleTime)
        {
            string timeString = idleTime.TotalHours >= 1 
                ? $"{(int)idleTime.TotalHours}h {idleTime.Minutes}m" 
                : $"{idleTime.Minutes}m";
            StatusText = $"Away ({timeString})";
        }

        private void OnInputActivity(object sender, PreProcessInputEventArgs e)
        {
            if (e.StagingItem.Input is MouseEventArgs || e.StagingItem.Input is KeyEventArgs)
            {
                _lastActivity = DateTime.Now;
                
                if (_warningShown)
                {
                    _warningShown = false;
                    SessionResumed?.Invoke(this, EventArgs.Empty);
                }

                if (IsAway)
                {
                    IsAway = false;
                    StatusText = "Active";
                    _ = _signalRService.UpdateStatusAsync("Online");
                }
            }
        }

        public void UpdateTimeout(int minutes)
        {
            LogoutThresholdMinutes = minutes;
            _localSettingsService.Settings.SessionTimeoutMinutes = minutes;
            _localSettingsService.Save();
        }

        public void Dispose()
        {
            _idleTimer.Stop();
            InputManager.Current.PreProcessInput -= OnInputActivity;
        }
    }
}
