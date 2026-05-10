using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;

namespace OCC.WpfClient.Features.Shell.ViewModels
{
    public partial class ShellViewModel : ViewModelBase, IRecipient<PreferenceChangedMessage>
    {
        private readonly LocalSettingsService _localSettings;

        [ObservableProperty]
        private INavigationService _navigation;

        [ObservableProperty]
        private double _themeBrightness = 0.5;

        public ShellViewModel(INavigationService navigation, LocalSettingsService localSettings)
        {
            _navigation = navigation;
            _localSettings = localSettings;
            Title = "Orange Circle Construction - ERP";

            _themeBrightness = _localSettings.Settings.ThemeBrightness;

            WeakReferenceMessenger.Default.Register<PreferenceChangedMessage>(this);
        }

        public void Receive(PreferenceChangedMessage message)
        {
            if (message.PreferenceName == nameof(LocalSettings.ThemeBrightness))
            {
                ThemeBrightness = _localSettings.Settings.ThemeBrightness;
            }
        }
    }
}
