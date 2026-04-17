using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Client.ViewModels.Notifications;
using System;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for the Order module's side menu, managing tab selection and notification access.
    /// </summary>
    public partial class OrderMenuViewModel : ViewModelBase, IRecipient<SwitchTabMessage>
    {
        #region Private Members
        #endregion

        #region Observables

        /// <summary>
        /// Gets or sets the currently active tab identifier.
        /// </summary>
        [ObservableProperty]
        private string _activeTab = "Dashboard";

        /// <summary>
        /// Gets or sets the display email of the currently authenticated user.
        /// </summary>
        [ObservableProperty]
        private string _userEmail = "origize63@gmail.com";
        
        [ObservableProperty] private bool _canViewDashboard = true;
        [ObservableProperty] private bool _canViewAllOrders = true;
        [ObservableProperty] private bool _canViewInventory = true;
        [ObservableProperty] private bool _canViewSuppliers = true;
        [ObservableProperty] private bool _canCreateOrders = true;
        [ObservableProperty] private bool _canViewPickingOrders = true;

        /// <summary>
        /// Gets the ViewModel for managing and displaying notifications.
        /// </summary>
        public NotificationViewModel NotificationVM { get; }

        private readonly IPermissionService _permissionService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderMenuViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="notificationVM">ViewModel for the notification system.</param>
        /// <param name="authService">Service for retrieving current user information.</param>
        /// <param name="permissionService">Service for checking granular access permissions.</param>
        /// <summary>
        /// Protected constructor for mocking.
        /// </summary>
        public OrderMenuViewModel() 
        { 
            NotificationVM = null!;
            _permissionService = null!;
        }

        public OrderMenuViewModel(NotificationViewModel notificationVM, IAuthService authService, IPermissionService permissionService)
        {
            NotificationVM = notificationVM;
            _permissionService = permissionService;
            
            WeakReferenceMessenger.Default.RegisterAll(this);
            
            if (authService.CurrentUser != null)
            {
                UserEmail = authService.CurrentUser.Email;
                InitializePermissions();
            }
        }

        private void InitializePermissions()
        {
            // Full access check (Orders)
            bool hasFullAccess = _permissionService.CanAccess(Infrastructure.NavigationRoutes.Feature_OrderManagement);
            // Inventory only check
            bool hasInventoryOnly = _permissionService.CanAccess(Infrastructure.NavigationRoutes.Feature_OrderInventoryOnly);

            // Logic: 
            // - Dashboard/All Orders/Suppliers/New Order = requires Full Access
            // - Inventory/Item List = requires Full Access OR Inventory Only
            
            CanViewDashboard = hasFullAccess;
            CanViewAllOrders = hasFullAccess;
            CanViewSuppliers = hasFullAccess;
            CanCreateOrders = hasFullAccess;
            
            CanViewInventory = hasFullAccess || hasInventoryOnly;
            CanViewPickingOrders = hasFullAccess || hasInventoryOnly;
            
            // Default active tab if Dashboard restricted
            if (!CanViewDashboard)
            {
                ActiveTab = "Inventory";
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to update the active tab and notify the parent container.
        /// </summary>
        /// <param name="tabName">The name of the tab to activate.</param>
        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
            TabSelected?.Invoke(this, tabName);
        }

        /// <summary>
        /// Command to request opening the global notifications overlay.
        /// </summary>
        [RelayCommand]
        private void OpenNotifications()
        {
            WeakReferenceMessenger.Default.Send(new OpenNotificationsMessage());
        }

        #endregion

        #region Methods

        /// <summary>
        /// Responds to external requests to switch tabs (e.g., from deep links).
        /// </summary>
        /// <param name="message">The tab switching request message.</param>
        public void Receive(SwitchTabMessage message)
        {
            ActiveTab = message.Value;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Event raised when a new tab is selected in the menu.
        /// </summary>
        public event EventHandler<string>? TabSelected;

        #endregion
    }
}
