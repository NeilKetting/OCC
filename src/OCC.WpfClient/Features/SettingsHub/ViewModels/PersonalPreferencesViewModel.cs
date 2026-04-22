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

        public PersonalPreferencesViewModel(LocalSettingsService localSettings)
        {
            _localSettings = localSettings;
            Title = "Personal Preferences";

            _maximizeOverTaskbar = _localSettings.Settings.MaximizeOverTaskbar;
        }

        [ObservableProperty]
        private bool _maximizeOverTaskbar;

        partial void OnMaximizeOverTaskbarChanged(bool value)
        {
            _localSettings.Settings.MaximizeOverTaskbar = value;
            _localSettings.Save();

            WeakReferenceMessenger.Default.Send(new PreferenceChangedMessage(nameof(LocalSettings.MaximizeOverTaskbar)));
        }
    }
}
