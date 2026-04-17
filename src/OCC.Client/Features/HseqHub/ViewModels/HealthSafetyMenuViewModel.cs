using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;

namespace OCC.Client.Features.HseqHub.ViewModels
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
            // Only handle if context is within HealthSafety, but SwitchTabMessage seems generic.
            // For now, accept it.
            ActiveTab = message.Value;
        }
    }
}
