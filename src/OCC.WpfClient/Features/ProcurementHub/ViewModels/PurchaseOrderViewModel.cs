using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.Models;
using OCC.WpfClient.Infrastructure;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class PurchaseOrderViewModel : OverlayHostViewModel
    {
        private readonly IOrderService _orderService;
        private readonly ISupplierService _supplierService;
        private readonly IProjectService _projectService;
        private readonly IInventoryService _inventoryService;
        private readonly INavigationService _navigationService;
        private readonly IPdfService _pdfService;
        private readonly IToastService _toastService;
        private readonly ILogger<PurchaseOrderViewModel> _logger;

        [ObservableProperty]
        private OrderWrapper? _currentOrder;

        [ObservableProperty]
        private ObservableCollection<Supplier> _suppliers = new();

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private ObservableCollection<InventoryItem> _inventoryItems = new();

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private Project? _selectedProject;

        private List<Guid> _allOrderIds = new();
        private int _currentIndex = -1;

        public PurchaseOrderViewModel(
            IOrderService orderService,
            ISupplierService supplierService,
            IProjectService projectService,
            IInventoryService inventoryService,
            INavigationService navigationService,
            IPdfService pdfService,
            IToastService toastService,
            ILogger<PurchaseOrderViewModel> logger)
        {
            _orderService = orderService;
            _supplierService = supplierService;
            _projectService = projectService;
            _inventoryService = inventoryService;
            _navigationService = navigationService;
            _pdfService = pdfService;
            _toastService = toastService;
            _logger = logger;

            Title = "Create Purchase Order";
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);
                
                var suppliersTask = _supplierService.GetSuppliersAsync();
                var projectsTask = _projectService.GetProjectsAsync();
                var inventoryTask = _inventoryService.GetInventoryAsync();

                var suppliers = await suppliersTask;
                var projects = await projectsTask;
                var inventory = await inventoryTask;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    Suppliers.Clear();
                    foreach (var s in suppliers) Suppliers.Add(s);

                    Projects.Clear();
                    foreach (var p in projects) Projects.Add(p);

                    InventoryItems.Clear();
                    foreach (var i in inventory) InventoryItems.Add(i);

                    if (CurrentOrder == null)
                    {
                        // Fetch all existing order IDs for cycling (newest first)
                        var allOrders = await _orderService.GetOrdersAsync();
                        _allOrderIds = allOrders.OrderByDescending(o => o.OrderDate).Select(o => o.Id).ToList();
                        
                        var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                        CurrentOrder = new OrderWrapper(order);
                        _currentIndex = -1; // -1 represents "New Order"
                        
                        // QuickBooks style: Pre-fill with 10 empty rows
                        for (int i = 0; i < 10; i++)
                        {
                            AddLine();
                        }
                    }
                });
            }
            catch (Exception)
            {
                _logger.LogError("Error creating new item");
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    ErrorMessage = "Failed to load required data. Please try again.");
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = false);
            }
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (value != null && CurrentOrder != null)
            {
                CurrentOrder.SupplierId = value.Id;
                CurrentOrder.SupplierName = value.Name;
                CurrentOrder.EntityAddress = value.Address;
                CurrentOrder.EntityTel = value.Phone;
                CurrentOrder.EntityVatNo = value.VatNumber;
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
            try
            {
                IsBusy = true;
                var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                
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
                _logger.LogError(ex, "Error saving order");
                ErrorMessage = "Failed to save the order.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveAndNewAsync()
        {
            if (CurrentOrder == null) return;
            try
            {
                IsBusy = true;
                var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                
                // Update cycling list for next time (even though we are resetting, it keeps the cache fresh)
                if (_currentIndex == -1)
                {
                    _allOrderIds.Insert(0, savedOrder.Id);
                }

                // Reset to new template
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                CurrentOrder = new OrderWrapper(order);
                SelectedSupplier = null;
                SelectedProject = null;
                _currentIndex = -1; // Ready for another new order
                
                for (int i = 0; i < 10; i++) AddLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving order");
                ErrorMessage = "Failed to save the order.";
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
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                CurrentOrder = new OrderWrapper(order);
                SelectedSupplier = null;
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
        private async Task CancelAsync()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        private bool _isShowingItemNotFoundDialog = false;

        [RelayCommand]
        private void UpdateLineItem(OrderLineWrapper line)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return;

            // Real-time update: Only update if we find a match. DO NOT show popup while typing.
            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.InventoryItemId = item.Id;
                line.Description = item.Description;
                line.UnitOfMeasure = item.UnitOfMeasure;
                line.UnitPrice = item.AverageCost;
                line.UpdateCalculations();
            }
        }

        [RelayCommand]
        private void ValidateLineItem(OrderLineWrapper line)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return;

            // FIX: If we just showed a dialog for THIS EXACT CODE and the user said No, don't nag them again.
            if (line.ItemCode.Equals(line.LastValidatedSku, StringComparison.OrdinalIgnoreCase)) return;

            // Final validation: If item not found, show the dialog.
            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.LastValidatedSku = line.ItemCode; // Mark as validated
                // Ensure everything is up to date
                line.InventoryItemId = item.Id;
                line.Description = item.Description;
                line.UnitOfMeasure = item.UnitOfMeasure;
                line.UnitPrice = item.AverageCost;
                line.UpdateCalculations();
            }
            else
            {
                if (_isShowingItemNotFoundDialog) return;
                _isShowingItemNotFoundDialog = true;
                line.LastValidatedSku = line.ItemCode; // Mark that we are prompting for this

                // Item not found - show dialog
                var dialog = new ItemNotFoundViewModel(line.ItemCode);
                dialog.Completed += (wantsToCreate) =>
                {
                    _isShowingItemNotFoundDialog = false;
                    CloseOverlay();
                    if (wantsToCreate)
                    {
                        ShowNewItemDialog(line);
                    }
                };
                OpenOverlay(dialog);
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
            if (_currentIndex == -1) // Currently on "New"
            {
                if (_allOrderIds.Count > 0)
                {
                    _currentIndex = 0; // Go to first (most recent)
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
                // Go back to the "New" template
                await ClearOrderAsync();
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
                    SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == order.SupplierId);
                    SelectedProject = Projects.FirstOrDefault(p => p.Id == order.ProjectId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order {Id}", id);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void FindOrder()
        {
            var dialog = new FindOrderViewModel(_orderService, _supplierService);
            dialog.CloseRequested += CloseOverlay;
            dialog.OrderSelected += (order) =>
            {
                CurrentOrder = new OrderWrapper(order);
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == order.SupplierId);
                SelectedProject = Projects.FirstOrDefault(p => p.Id == order.ProjectId);
                
                // Update index for cycling
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
                
                // Open the PDF using default OS viewer
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing order");
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
            // For now, same as preview (User can print from PDF viewer)
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
                
                var subject = Uri.EscapeDataString($"Purchase Order {CurrentOrder.OrderNumber} - Onsite Construction Care");
                var body = Uri.EscapeDataString($"Please find attached Purchase Order {CurrentOrder.OrderNumber}.");
                var mailto = $"mailto:?subject={subject}&body={body}";
                
                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
                
                // Note: Standard mailto doesn't support attachments in a cross-platform/cross-client way reliably.
                // In a production app, we'd use MAPI or an SMTP client if needed, or prompt the user.
                _toastService.ShowInfo("Email", "Default mail client opened. Please attach the generated PDF from your Documents folder.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emailing order");
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
            if (CurrentOrder == null) return;
            // logic to delete current draft if needed
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        private void ShowNewItemDialog(OrderLineWrapper line)
        {
            var dialog = new NewItemViewModel(line.ItemCode, _inventoryService);
            dialog.Completed += (newItem) =>
            {
                CloseOverlay();
                if (newItem != null)
                {
                    // Item was already saved in NewItemViewModel
                    // Update local lists
                    InventoryItems.Add(newItem);
                    
                    // Update line
                    line.ItemCode = newItem.Sku;
                    line.InventoryItemId = newItem.Id;
                    line.Description = newItem.Description;
                    line.UnitOfMeasure = newItem.UnitOfMeasure;
                    line.UnitPrice = newItem.Price;
                    line.UpdateCalculations();
                }
            };
            OpenOverlay(dialog);
        }
    }
}
