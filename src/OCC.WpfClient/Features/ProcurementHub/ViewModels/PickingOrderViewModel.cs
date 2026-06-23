using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;
using System.Diagnostics;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class PickingOrderViewModel : OverlayHostViewModel
    {
        private readonly IOrderService _orderService;
        private readonly IProjectService _projectService;
        private readonly IInventoryService _inventoryService;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ISupplierService _supplierService;
        private readonly IPdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<PickingOrderViewModel> _logger;

        [ObservableProperty]
        private OrderWrapper? _currentOrder;

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private ObservableCollection<InventoryItem> _inventoryItems = new();

        [ObservableProperty]
        private Project? _selectedProject;

        private List<Guid> _allOrderIds = new();
        private int _currentIndex = -1;

        public PickingOrderViewModel(
            IOrderService orderService,
            IProjectService projectService,
            IInventoryService inventoryService,
            IAuthService authService,
            IToastService toastService,
            ISupplierService supplierService,
            IPdfService pdfService,
            IDialogService dialogService,
            ILogger<PickingOrderViewModel> logger)
        {
            _orderService = orderService;
            _projectService = projectService;
            _inventoryService = inventoryService;
            _authService = authService;
            _toastService = toastService;
            _supplierService = supplierService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _logger = logger;

            Title = "Stock Picking & Project Allocation";
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);

                var projectsTask = _projectService.GetProjectsAsync();
                var inventoryTask = _inventoryService.GetInventoryAsync();

                var projects = await projectsTask;
                var inventory = await inventoryTask;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    Projects.Clear();
                    foreach (var p in projects) Projects.Add(p);

                    // Filter inventory by branch stock
                    var branch = _authService.CurrentUser?.Branch ?? Branch.JHB;
                    var filteredInventory = inventory.Where(i =>
                        (branch == Branch.JHB && i.JhbQuantity > 0) ||
                        (branch == Branch.CPT && i.CptQuantity > 0))
                        .ToList();

                    InventoryItems.Clear();
                    foreach (var i in filteredInventory) InventoryItems.Add(i);

                    if (CurrentOrder == null)
                    {
                        // Fetch all existing picking order IDs for cycling (newest first)
                        var allOrders = await _orderService.GetOrdersAsync();
                        _allOrderIds = allOrders.Where(o => o.OrderType == OrderType.PickingOrder)
                                                .OrderByDescending(o => o.OrderDate)
                                                .Select(o => o.Id)
                                                .ToList();

                        var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PickingOrder);
                        order.DestinationType = OrderDestinationType.Site;
                        if (_authService.CurrentUser?.Branch != null)
                        {
                            order.Branch = _authService.CurrentUser.Branch.Value;
                        }

                        CurrentOrder = new OrderWrapper(order);
                        _currentIndex = -1; // -1 represents "New Order"

                        // Pre-fill with 10 empty rows
                        for (int i = 0; i < 10; i++)
                        {
                            AddLine();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load picking order data");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ErrorMessage = "Failed to load required data. Please try again.");
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = false);
            }
        }

        partial void OnSelectedProjectChanged(Project? value)
        {
            if (value != null && CurrentOrder != null)
            {
                CurrentOrder.ProjectId = value.Id;
                CurrentOrder.ProjectName = value.Name;
                CurrentOrder.Attention = value.ProjectManager ?? string.Empty;
            }
        }

        [RelayCommand]
        private void AddLine()
        {
            if (CurrentOrder == null) return;

            var newline = new OrderLine
            {
                Id = Guid.NewGuid(),
                OrderId = CurrentOrder.Id,
                QuantityOrdered = 0,
                UnitPrice = 0
            };

            CurrentOrder.Lines.Add(new OrderLineWrapper(newline, CurrentOrder));
        }

        [RelayCommand]
        private void RemoveLine(OrderLineWrapper line)
        {
            CurrentOrder?.Lines.Remove(line);
        }

        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            if (CurrentOrder == null) return;

            if (SelectedProject == null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ErrorMessage = "Please select a Project/Site for the picking order.");
                return;
            }

            var validLines = CurrentOrder.Lines.Where(l => l.IsItemValid && l.QuantityOrdered > 0).ToList();
            if (!validLines.Any())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ErrorMessage = "Please add at least one item with a valid pick quantity.");
                return;
            }

            try
            {
                IsBusy = true;

                // Clear empty lines and commit
                CurrentOrder.Model.Lines.Clear();
                foreach (var line in validLines)
                {
                    line.UnitPrice = 0;
                    CurrentOrder.Model.Lines.Add(line.Model);
                }

                var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                _toastService.ShowSuccess("Success", "Picking order created successfully.");

                // Update cycling list
                if (_currentIndex == -1)
                {
                    _allOrderIds.Insert(0, savedOrder.Id);
                    _currentIndex = 0;
                }

                WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
                WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving picking order");
                ErrorMessage = "Failed to save the picking order.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ClearOrderAsync()
        {
            try
            {
                IsBusy = true;
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PickingOrder);
                order.DestinationType = OrderDestinationType.Site;
                if (_authService.CurrentUser?.Branch != null)
                {
                    order.Branch = _authService.CurrentUser.Branch.Value;
                }

                CurrentOrder = new OrderWrapper(order);
                SelectedProject = null;
                _currentIndex = -1;
                for (int i = 0; i < 10; i++) AddLine();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        [RelayCommand]
        private void UpdateLineItem(OrderLineWrapper line)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return;

            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.InventoryItemId = item.Id;
                line.Description = item.Description;
                line.UnitOfMeasure = item.UnitOfMeasure;
                line.UnitPrice = 0;
                line.UpdateCalculations();
            }
        }

        [RelayCommand]
        private void ValidateLineItem(OrderLineWrapper line)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return;
            if (line.ItemCode.Equals(line.LastValidatedSku, StringComparison.OrdinalIgnoreCase)) return;

            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.LastValidatedSku = line.ItemCode;
                line.InventoryItemId = item.Id;
                line.Description = item.Description;
                line.UnitOfMeasure = item.UnitOfMeasure;
                line.UnitPrice = 0;
                line.UpdateCalculations();
            }
            else
            {
                line.LastValidatedSku = line.ItemCode;
                _toastService.ShowWarning("Item Not Found", $"Item code '{line.ItemCode}' is not available in your branch stock.");
                line.InventoryItemId = null;
                line.Description = string.Empty;
                line.UnitOfMeasure = string.Empty;
                line.UpdateCalculations();
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        [RelayCommand]
        private async Task PreviousOrderAsync()
        {
            if (_currentIndex == -1)
            {
                if (_allOrderIds.Count > 0)
                {
                    _currentIndex = 0;
                    await LoadOrderByIdAsync(_allOrderIds[_currentIndex]);
                }
            }
            else if (_currentIndex < _allOrderIds.Count - 1)
            {
                _currentIndex++;
                await LoadOrderByIdAsync(_allOrderIds[_currentIndex]);
            }
        }

        [RelayCommand]
        private async Task NextOrderAsync()
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                await LoadOrderByIdAsync(_allOrderIds[_currentIndex]);
            }
            else if (_currentIndex == 0)
            {
                await ClearOrderAsync();
            }
        }

        public async Task LoadOrderAsync(Guid id)
        {
            try
            {
                IsBusy = true;

                if (!Projects.Any())
                {
                    var projects = await _projectService.GetProjectsAsync();
                    var inventory = await _inventoryService.GetInventoryAsync();

                    Projects.Clear();
                    foreach (var p in projects) Projects.Add(p);

                    var branch = _authService.CurrentUser?.Branch ?? Branch.JHB;
                    var filteredInventory = inventory.Where(i =>
                        (branch == Branch.JHB && i.JhbQuantity > 0) ||
                        (branch == Branch.CPT && i.CptQuantity > 0))
                        .ToList();

                    InventoryItems.Clear();
                    foreach (var i in filteredInventory) InventoryItems.Add(i);

                    var allOrders = await _orderService.GetOrdersAsync();
                    _allOrderIds = allOrders.Where(o => o.OrderType == OrderType.PickingOrder)
                                            .OrderByDescending(o => o.OrderDate)
                                            .Select(o => o.Id)
                                            .ToList();
                }

                var order = await _orderService.GetOrderAsync(id);
                if (order != null)
                {
                    CurrentOrder = new OrderWrapper(order);
                    SelectedProject = Projects.FirstOrDefault(p => p.Id == order.ProjectId);
                    _currentIndex = _allOrderIds.IndexOf(order.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading picking order {Id}", id);
                ErrorMessage = "Failed to load picking order details.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadOrderByIdAsync(Guid id)
        {
            try
            {
                IsBusy = true;
                var order = await _orderService.GetOrderAsync(id);
                if (order != null)
                {
                    CurrentOrder = new OrderWrapper(order);
                    SelectedProject = Projects.FirstOrDefault(p => p.Id == order.ProjectId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading picking order {Id}", id);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void FindOrder()
        {
            var dialog = new FindOrderViewModel(_orderService, _supplierService, "Picking Order");
            dialog.CloseRequested += CloseOverlay;
            dialog.OrderSelected += (order) =>
            {
                CurrentOrder = new OrderWrapper(order);
                SelectedProject = Projects.FirstOrDefault(p => p.Id == order.ProjectId);
                _currentIndex = _allOrderIds.IndexOf(order.Id);
                CloseOverlay();
            };
            OpenOverlay(dialog);
        }

        [RelayCommand]
        private async Task PreviewOrderAsync()
        {
            if (CurrentOrder == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating PDF...";
                var path = await _pdfService.GenerateOrderPdfAsync(CurrentOrder.Model);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing picking order");
                ErrorMessage = "Failed to generate PDF preview.";
            }
            finally
            {
                IsBusy = false;
                BusyText = string.Empty;
            }
        }

        [RelayCommand]
        private async Task PrintOrderAsync()
        {
            await PreviewOrderAsync();
        }

        [RelayCommand]
        private async Task EmailOrderAsync()
        {
            if (CurrentOrder == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Preparing email...";
                var path = await _pdfService.GenerateOrderPdfAsync(CurrentOrder.Model);
                
                var subject = Uri.EscapeDataString($"Picking Order {CurrentOrder.OrderNumber} - Onsite Construction Care");
                var body = Uri.EscapeDataString($"Please find attached Picking Order {CurrentOrder.OrderNumber}.");
                var mailto = $"mailto:?subject={subject}&body={body}";
                
                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
                _toastService.ShowInfo("Email", "Default mail client opened. Please attach the generated PDF from your Documents folder.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emailing picking order");
                ErrorMessage = "Failed to prepare email.";
            }
            finally
            {
                IsBusy = false;
                BusyText = string.Empty;
            }
        }

        [RelayCommand]
        private async Task DeleteOrderAsync()
        {
            if (CurrentOrder == null || _currentIndex == -1) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Picking Order", 
                $"Are you sure you want to delete picking order {CurrentOrder.OrderNumber}? This action cannot be undone.");
            
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                await _orderService.DeleteOrderAsync(CurrentOrder.Id);
                _toastService.ShowSuccess("Success", "Picking order deleted successfully.");
                
                WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
                WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting picking order");
                ErrorMessage = "Failed to delete the picking order.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
