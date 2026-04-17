using Microsoft.Extensions.Logging;
using OCC.Client.Features.OrdersHub.ViewModels;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public interface IOrderLifecycleService
    {
        Task LoadInitialDataAsync(CreateOrderViewModel coordinator);
        Task LoadOrderAsync(CreateOrderViewModel coordinator, Order order);
        void Reset(CreateOrderViewModel coordinator);
        Task RestoreStateAsync(CreateOrderViewModel coordinator);
    }

    public class OrderLifecycleService : IOrderLifecycleService
    {
        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly OrderStateService _orderStateService;
        private readonly ILogger<OrderLifecycleService> _logger;

        public OrderLifecycleService(
            IOrderManager orderManager,
            IDialogService dialogService,
            IAuthService authService,
            OrderStateService orderStateService,
            ILogger<OrderLifecycleService> logger)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _authService = authService;
            _orderStateService = orderStateService;
            _logger = logger;
        }

        public async Task LoadInitialDataAsync(CreateOrderViewModel vm)
        {
            try
            {
                vm.IsBusy = true;
                vm.BusyText = "Loading order entry data...";
                
                var data = await _orderManager.GetOrderEntryDataAsync();

                vm.Suppliers.Initialize(data.Suppliers, vm.CurrentOrder?.Branch ?? Branch.CPT);
                
                vm.Customers.Clear();
                foreach(var i in data.Customers) vm.Customers.Add(i);

                vm.Projects.Clear();
                foreach(var i in data.Projects) vm.Projects.Add(i);

                vm.Inventory.Initialize(data.Inventory);
                vm.Lines.SetInventoryMaster(data.Inventory);

                if (_orderStateService.HasSavedState) 
                {
                    await RestoreStateAsync(vm);
                }
                else if (vm.CurrentOrder == null || string.IsNullOrEmpty(vm.CurrentOrder.Model.OrderNumber) || vm.CurrentOrder.Model.OrderNumber == "DESIGN-TIME")
                {
                    Reset(vm);
                }
            }
            catch(Exception ex)
            {
                  _logger.LogError(ex, "Failed to load order entry data");
                  await _dialogService.ShowAlertAsync("Error", "Check connection and try again.");
            }
            finally { vm.IsBusy = false; }
        }

        public async Task LoadOrderAsync(CreateOrderViewModel vm, Order order)
        {
            try
            {
                vm.IsBusy = true;
                vm.BusyText = "Loading order details...";

                Order fullOrder = order;
                if (order.Id != Guid.Empty)
                {
                    fullOrder = await _orderManager.GetOrderByIdAsync(order.Id) ?? order;
                }

                vm.PrepareForOrder(fullOrder);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to load order details");
                await _dialogService.ShowAlertAsync("Error", "Failed to load order details.");
            }
            finally { vm.IsBusy = false; }
        }

        public void Reset(CreateOrderViewModel vm)
        {
            var template = _orderManager.CreateNewOrderTemplate();
            
            // Set default branch from user
            if (_authService?.CurrentUser?.Branch != null)
            {
                template.Branch = _authService.CurrentUser.Branch.Value;
            }

            vm.PrepareForOrder(template, isNew: true);
            _orderStateService.ClearState();
        }

        public async Task RestoreStateAsync(CreateOrderViewModel vm)
        {
            var (savedOrder, pendingLine, searchTerm) = _orderStateService.RetrieveState();
            if (savedOrder != null)
            {
                await LoadOrderAsync(vm, savedOrder);
                
                if (pendingLine != null) vm.Lines.NewLine = new OrderLineWrapper(pendingLine);
                
                if (!string.IsNullOrEmpty(searchTerm))
                {
                   var match = vm.Inventory.FilteredItems.FirstOrDefault(i => 
                        i.Sku.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                        i.Description.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));
                   
                   if (match != null) 
                   {
                       vm.Inventory.SelectedItem = match;
                       await vm.Lines.AddLine();
                   }
                }
            }
            vm.Lines.EnsureBlankRow();
            _orderStateService.ClearState();
        }
    }
}
