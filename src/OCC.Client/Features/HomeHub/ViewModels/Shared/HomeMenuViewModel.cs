using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;

using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Notifications;

namespace OCC.Client.Features.HomeHub.ViewModels.Shared
{
    public partial class HomeMenuViewModel : ViewModelBase, IRecipient<SwitchTabMessage>
    {
        #region Observables

        [ObservableProperty]
        private string _activeTab = "My Summary";

        [ObservableProperty]
        private string _userEmail = "origize63@gmail.com";

        #endregion

        #region Constructors

        public NotificationViewModel NotificationVM { get; }
        private readonly Services.Interfaces.IPermissionService _permissionService;
        public bool CanAccessCalendar => _permissionService.CanAccess("Calendar");

        public HomeMenuViewModel(NotificationViewModel notificationVM, Services.Interfaces.IPermissionService permissionService)
        {
            NotificationVM = notificationVM;
            _permissionService = permissionService;
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
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
