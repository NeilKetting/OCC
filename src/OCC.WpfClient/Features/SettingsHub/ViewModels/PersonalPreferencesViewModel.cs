using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.SettingsHub.ViewModels
{
    public partial class PersonalPreferencesViewModel : ViewModelBase
    {
        private readonly LocalSettingsService _localSettings;
        private readonly UserActivityService _userActivityService;

        public PersonalPreferencesViewModel(LocalSettingsService localSettings, UserActivityService userActivityService)
        {
            _localSettings = localSettings;
            _userActivityService = userActivityService;
            Title = "Personal Preferences";

            _maximizeOverTaskbar = _localSettings.Settings.MaximizeOverTaskbar;
            _sessionTimeoutMinutes = _localSettings.Settings.SessionTimeoutMinutes;
        }

        [ObservableProperty]
        private bool _maximizeOverTaskbar;

        [ObservableProperty]
        private int _sessionTimeoutMinutes;

        partial void OnMaximizeOverTaskbarChanged(bool value)
        {
            _localSettings.Settings.MaximizeOverTaskbar = value;
            _localSettings.Save();

            WeakReferenceMessenger.Default.Send(new PreferenceChangedMessage(nameof(LocalSettings.MaximizeOverTaskbar)));
        }

        partial void OnSessionTimeoutMinutesChanged(int value)
        {
            _userActivityService.UpdateTimeout(value);
        }
    }
}
