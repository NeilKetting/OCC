using OCC.Client.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;
using OCC.Client.ViewModels.Notifications;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectMainMenuViewModel : ViewModelBase, IRecipient<SwitchTabMessage>
    {
        #region Observables

        [ObservableProperty]
        private string _activeTab = "Projects";

        [ObservableProperty]
        private string _userEmail = "origize63@gmail.com";

        #endregion

        #region Constructors

        public NotificationViewModel NotificationVM { get; }

        public ProjectMainMenuViewModel(NotificationViewModel notificationVM)
        {
            NotificationVM = notificationVM;
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
            // Send explicit message to force navigation even if tab is already active
            WeakReferenceMessenger.Default.Send(new SwitchTabMessage(tabName));
        }

        [RelayCommand]
        private void OpenNotifications()
        {
            WeakReferenceMessenger.Default.Send(new OpenNotificationsMessage());
        }

        #endregion

        #region Methods

        public void Receive(SwitchTabMessage message)
        {
            ActiveTab = message.Value;
        }

        #endregion
    }
}




