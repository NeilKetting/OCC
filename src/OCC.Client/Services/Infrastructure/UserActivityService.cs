using System;
using Avalonia.Input;
using Avalonia.Controls; // Added for TopLevel
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.Client.Services.Infrastructure
{
    public partial class UserActivityService : ObservableObject, IDisposable
    {
        private readonly DispatcherTimer _idleTimer;
        private DateTime _lastActivity = DateTime.Now;
        private const int IdleThresholdMinutes = 1; // "Away" status

        // Dynamic properties
        public double LogoutThresholdMinutes { get; set; } = 5.0;
        public double WarningThresholdMinutes => Math.Max(0.5, LogoutThresholdMinutes - 1.0);

        [ObservableProperty]
        private bool _isAway;

        [ObservableProperty]
        private string _statusText = "Active";

        private readonly SignalRNotificationService _signalRService;
        private readonly UserPreferencesService _userPreferencesService;

        public UserActivityService(SignalRNotificationService signalRService, UserPreferencesService userPreferencesService)
        {
            _signalRService = signalRService;
            _userPreferencesService = userPreferencesService;
            
            // Load timeout from preferences
            LogoutThresholdMinutes = _userPreferencesService.Preferences.SessionTimeoutMinutes;

            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _idleTimer.Tick += CheckIdleStatus;
            _idleTimer.Start();
        }

        public void Monitor(Avalonia.Controls.TopLevel topLevel)
        {
            if (topLevel != null)
            {
                topLevel.AddHandler(InputElement.PointerMovedEvent, OnInputActivity, Avalonia.Interactivity.RoutingStrategies.Tunnel);
                topLevel.AddHandler(InputElement.KeyDownEvent, OnInputActivity, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
        }

        public event EventHandler? SessionWarning;
        public event EventHandler? SessionExpired;
        public event EventHandler? SessionResumed; // To close dialog if they move mouse

        private bool _warningShown;
        
        private async void CheckIdleStatus(object? sender, EventArgs e)
        {
            var idleTime = DateTime.Now - _lastActivity;
            
            // 1. Session Timeout Logic
            if (idleTime.TotalMinutes >= LogoutThresholdMinutes)
            {
                SessionExpired?.Invoke(this, EventArgs.Empty);
                _idleTimer.Stop(); // Stop checking once expired
                return;
            }

            if (idleTime.TotalMinutes >= WarningThresholdMinutes && !_warningShown)
            {
                _warningShown = true;
                SessionWarning?.Invoke(this, EventArgs.Empty);
            }

            // 2. Existing Away Logic (Keep 1m for "Away" status)
            if (idleTime.TotalMinutes >= IdleThresholdMinutes && !IsAway)
            {
                IsAway = true;
                string timeString = idleTime.TotalHours >= 1 
                    ? $"{(int)idleTime.TotalHours}h {idleTime.Minutes}m" 
                    : $"{idleTime.Minutes}m";
                
                StatusText = $"Away ({timeString})";
                await _signalRService.UpdateStatusAsync("Away");
            }
            else if (IsAway)
            {
                 // Update time string while away
                string timeString = idleTime.TotalHours >= 1 
                    ? $"{(int)idleTime.TotalHours}h {idleTime.Minutes}m" 
                    : $"{idleTime.Minutes}m";
                StatusText = $"Away ({timeString})";
            }
        }

        private async void OnInputActivity(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _lastActivity = DateTime.Now;
            
            // If Warning was shown and we are now active => Resume
            if (_warningShown)
            {
                _warningShown = false;
                SessionResumed?.Invoke(this, EventArgs.Empty);
            }

            if (IsAway)
            {
                IsAway = false;
                StatusText = "Active";
                await _signalRService.UpdateStatusAsync("Online");
            }
        }

        public void UpdateTimeout(int minutes)
        {
            LogoutThresholdMinutes = minutes;
            // Also update preferences service
            _userPreferencesService.Preferences.SessionTimeoutMinutes = minutes;
            _userPreferencesService.SavePreferences();
        }

        public void Dispose()
        {
            _idleTimer.Stop();
        }
    }
}
