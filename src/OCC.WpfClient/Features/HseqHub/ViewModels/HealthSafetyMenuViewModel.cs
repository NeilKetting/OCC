using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class HealthSafetyMenuViewModel : ViewModelBase, IRecipient<SwitchTabMessage>
    {
        [ObservableProperty]
        private string _activeTab = "Dashboard";

        public HealthSafetyMenuViewModel()
        {
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
        }

        public void Receive(SwitchTabMessage message)
        {
            ActiveTab = message.Value;
        }
    }
}
