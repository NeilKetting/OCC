using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class ProcurementViewModel : OverlayHostViewModel
    {
        private readonly ILogger<ProcurementViewModel> _logger;
        private readonly INavigationService _navigationService;
        private readonly IOrderService _orderService;
        private readonly ISupplierService _supplierService;
        private readonly IServiceProvider _serviceProvider;

        public ProcurementViewModel(
            ILogger<ProcurementViewModel> logger, 
            INavigationService navigationService,
            IOrderService orderService,
            ISupplierService supplierService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _navigationService = navigationService;
            _orderService = orderService;
            _supplierService = supplierService;
            _serviceProvider = serviceProvider;
            Title = "Procurement Overview";
            _logger.LogInformation("ProcurementViewModel initialized");
        }

        [RelayCommand]
        private void NavigateToPurchaseOrder()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.PurchaseOrder));
        }

        [RelayCommand]
        private void ReceiveStock()
        {
            var findOrderVm = new FindOrderViewModel(_orderService, _supplierService);
            findOrderVm.CloseRequested += CloseOverlay;
            findOrderVm.OrderSelected += (order) =>
            {
                CloseOverlay();
                ShowReceiveStockDialog(order);
            };
            OpenOverlay(findOrderVm);
        }

        private void ShowReceiveStockDialog(Order order)
        {
            var receiveVm = (Dialogs.ReceiveStockViewModel)System.Windows.Application.Current.Dispatcher.Invoke(() => _serviceProvider.GetRequiredService<Dialogs.ReceiveStockViewModel>());
            receiveVm.LoadOrder(order);
            receiveVm.CloseRequested += CloseOverlay;
            receiveVm.OrderReceived += () => 
            {
                // Refresh logic if needed
            };
            OpenOverlay(receiveVm);
        }
    }
}
