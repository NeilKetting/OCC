using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.WagesHub.ViewModels
{
    public partial class WagesMenuViewModel : ViewModelBase, IRecipient<SwitchTabMessage>
    {
        [ObservableProperty]
        private string _activeTab = "WageRun";

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
