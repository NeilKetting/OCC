using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    public partial class PickingOrderViewModel : ViewModelBase
    {
        private readonly IOrderManager _orderManager;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<PickingOrderViewModel> _logger;

        [ObservableProperty]
        private OrderWrapper _currentOrder = null!;

        [ObservableProperty]
        private bool _isReadOnly;

        public InventoryLookupViewModel InventoryLookup { get; }

        public PickingOrderViewModel(
            IOrderManager orderManager,
            IAuthService authService,
            IDialogService dialogService,
            ILogger<PickingOrderViewModel> logger,
            InventoryLookupViewModel inventoryLookup)
        {
            _orderManager = orderManager;
            _authService = authService;
            _dialogService = dialogService;
            _logger = logger;
            InventoryLookup = inventoryLookup;

            Reset();
        }

        public void Reset()
        {
            var order = _orderManager.CreateNewOrderTemplate(OrderType.PickingOrder);
            order.DestinationType = OrderDestinationType.Site;
            
            if (_authService.CurrentUser?.Branch != null)
            {
                order.Branch = _authService.CurrentUser.Branch.Value;
            }

            CurrentOrder = new OrderWrapper(order);
            CurrentOrder.PropertyChanged += OnOrderPropertyChanged;
        }

        private void OnOrderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Handle side effects if needed
        }

        [RelayCommand]
        public async Task LoadData()
        {
            try
            {
                // Load Projects
                var orderData = await _orderManager.GetOrderEntryDataAsync();
                Projects.Clear();
                foreach (var p in orderData.Projects) Projects.Add(p);

                // Filter inventory by branch stock
                var data = await _orderManager.GetOrderEntryDataAsync(); // Already have it, but let's be safe or reuse it
                var branch = _authService.CurrentUser?.Branch ?? Branch.JHB;
                var filteredInventory = data.Inventory.Where(i => 
                    (branch == Branch.JHB && i.JhbQuantity > 0) || 
                    (branch == Branch.CPT && i.CptQuantity > 0))
                    .ToList();

                InventoryLookup.Initialize(filteredInventory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load picking order data");
                await _dialogService.ShowAlertAsync("Error", "Failed to load inventory data.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void AddLine()
        {
            if (InventoryLookup.SelectedItem == null) return;

            var item = InventoryLookup.SelectedItem;
            
            // Validation: Check if already added
            if (CurrentOrder.Lines.Any(l => l.ItemCode == item.Sku))
            {
                _ = _dialogService.ShowAlertAsync("Info", $"Item {item.Sku} already in list.");
                return;
            }

            var newLine = new OrderLine
            {
                OrderId = CurrentOrder.Id,
                InventoryItemId = item.Id,
                ItemCode = item.Sku,
                Description = item.Description,
                Category = item.Category,
                QuantityOrdered = 1,
                UnitOfMeasure = item.UnitOfMeasure,
                UnitPrice = 0, // No pricing for picking
            };

            CurrentOrder.Lines.Add(new OrderLineWrapper(newLine));
            
            // Use Post to ensure this happens after any UI events (like Enter key handling in AutoCompleteBox)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                InventoryLookup.SelectedItem = null;
                InventoryLookup.SearchText = string.Empty;
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        public ObservableCollection<Project> Projects { get; } = new();

        [ObservableProperty]
        private Project? _selectedProject;

        partial void OnSelectedProjectChanged(Project? value)
        {
            if (CurrentOrder != null)
            {
                CurrentOrder.ProjectId = value?.Id;
                CurrentOrder.ProjectName = value?.Name ?? string.Empty;
            }
        }

        [RelayCommand]
        public void RemoveLine(OrderLineWrapper line)
        {
            CurrentOrder.Lines.Remove(line);
        }

        [RelayCommand]
        public async Task SubmitOrder()
        {
            if (!ValidateOrder(out string error))
            {
                await _dialogService.ShowAlertAsync("Validation Error", error);
                return;
            }

            try
            {
                IsBusy = true;
                CurrentOrder.CommitToModel();
                await _orderManager.CreateOrderAsync(CurrentOrder.Model);
                
                await _dialogService.ShowAlertAsync("Success", "Picking order created successfully.");
                OrderCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit picking order");
                await _dialogService.ShowAlertAsync("Error", "Failed to submit order.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateOrder(out string error)
        {
            error = string.Empty;

            if (CurrentOrder.Lines.Count == 0)
            {
                error = "Please add at least one item to pick.";
                return false;
            }

            if (SelectedProject == null)
            {
                error = "Please select a Project/Site for the picking order.";
                return false;
            }

            return true;
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? OrderCreated;
        public event EventHandler? CloseRequested;
    }
}
