using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.Shared.Interfaces;
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
    public partial class PurchaseOrderDetailViewModel : OverlayHostViewModel
    {
        private readonly IOrderService _orderService;
        private readonly ISupplierService _supplierService;
        private readonly IProjectService _projectService;
        private readonly IInventoryService _inventoryService;
        private readonly INavigationService _navigationService;
        private readonly IPdfService _pdfService;
        private readonly IToastService _toastService;
        private readonly IGoogleMapsService _googleMapsService;
        private readonly ISettingsService _settingsService;
        private readonly IAuthService _authService;
        private readonly OCC.WpfClient.Services.Infrastructure.ConnectionSettings _connectionSettings;
        private readonly ILogger<PurchaseOrderDetailViewModel> _logger;

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
        [ObservableProperty]
        private bool _isNewOrder = true;

        [ObservableProperty]
        private Guid? _orderId;

        [ObservableProperty]
        private AddressSuggestion? _selectedAddressSuggestion;

        [ObservableProperty]
        private bool _isAddressFocused;

        public ObservableCollection<AddressSuggestion> AddressSuggestions { get; } = new();

        private string _addressSessionToken = Guid.NewGuid().ToString();
        private System.Threading.CancellationTokenSource? _addressCts;
        private bool _isHandlingAddressSelection;

        public PurchaseOrderDetailViewModel(
            IOrderService orderService,
            ISupplierService supplierService,
            IProjectService projectService,
            IInventoryService inventoryService,
            INavigationService navigationService,
            IPdfService pdfService,
            IToastService toastService,
            IGoogleMapsService googleMapsService,
            ISettingsService settingsService,
            IAuthService authService,
            OCC.WpfClient.Services.Infrastructure.ConnectionSettings connectionSettings,
            ILogger<PurchaseOrderDetailViewModel> logger)
        {
            _orderService = orderService;
            _supplierService = supplierService;
            _projectService = projectService;
            _inventoryService = inventoryService;
            _navigationService = navigationService;
            _pdfService = pdfService;
            _toastService = toastService;
            _googleMapsService = googleMapsService;
            _settingsService = settingsService;
            _authService = authService;
            _connectionSettings = connectionSettings;
            _logger = logger;

            Title = "Create Purchase Order";
        }

        partial void OnCurrentOrderChanged(OrderWrapper? oldValue, OrderWrapper? newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= CurrentOrder_PropertyChanged;
            }
            if (newValue != null)
            {
                newValue.PropertyChanged += CurrentOrder_PropertyChanged;
            }
        }

        private async void CurrentOrder_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isHandlingAddressSelection) return;

            if (e.PropertyName == nameof(OrderWrapper.DeliveryInstructions))
            {
                if (CurrentOrder != null && CurrentOrder.IsOtherSelected)
                {
                    await UpdateAddressSuggestionsAsync();
                }
            }
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsBusy) return;
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);
                
                // 1. Load lookups sequentially
                if (!Suppliers.Any() || !Projects.Any() || !InventoryItems.Any())
                {
                    var suppliersTask = _supplierService.GetSuppliersAsync();
                    var projectsTask = _projectService.GetProjectsAsync();
                    var inventoryTask = _inventoryService.GetInventoryAsync();

                    var suppliers = await suppliersTask;
                    var projects = await projectsTask;
                    var inventory = await inventoryTask;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!Suppliers.Any())
                        {
                            Suppliers.Clear();
                            foreach (var s in suppliers) Suppliers.Add(s);
                        }

                        if (!Projects.Any())
                        {
                            Projects.Clear();
                            foreach (var p in projects) Projects.Add(p);
                        }

                        if (!InventoryItems.Any())
                        {
                            InventoryItems.Clear();
                            foreach (var i in inventory) InventoryItems.Add(i);
                        }
                    });
                }

                // 2. Fetch all existing order IDs for cycling (newest first)
                if (_allOrderIds == null || !_allOrderIds.Any())
                {
                    var allOrders = await _orderService.GetOrdersAsync();
                    _allOrderIds = allOrders.OrderByDescending(o => o.OrderDate).Select(o => o.Id).ToList();
                }

                // 3. Populate or create order
                if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                {
                    // Load existing order
                    var order = await _orderService.GetOrderAsync(OrderId.Value);
                    if (order != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentOrder = new OrderWrapper(order);
                            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == order.SupplierId);
                            SelectedProject = Projects.FirstOrDefault(p => p.Id == order.ProjectId);
                            _currentIndex = _allOrderIds.IndexOf(order.Id);
                            IsNewOrder = false;
                        });
                    }
                }
                else if (CurrentOrder == null)
                {
                    // Create new order template
                    var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                    if (_authService.CurrentUser?.Branch != null)
                    {
                        order.Branch = _authService.CurrentUser.Branch.Value;
                    }
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentOrder = new OrderWrapper(order);
                        _currentIndex = -1; // -1 represents "New Order"
                        IsNewOrder = true;
                        
                        // QuickBooks style: Pre-fill with 10 empty rows
                        for (int i = 0; i < 10; i++)
                        {
                            AddLine();
                        }
                    });
                }
                else
                {
                    // If CurrentOrder is already present, synchronize dropdowns
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (SelectedSupplier == null && CurrentOrder.SupplierId != Guid.Empty)
                        {
                            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == CurrentOrder.SupplierId);
                        }
                        if (SelectedProject == null && CurrentOrder.ProjectId.HasValue && CurrentOrder.ProjectId.Value != Guid.Empty)
                        {
                            SelectedProject = Projects.FirstOrDefault(p => p.Id == CurrentOrder.ProjectId.Value);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase order details data");
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
                
                if (IsNewOrder)
                {
                    var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                    
                    // Update cycling list
                    if (_currentIndex == -1)
                    {
                        _allOrderIds.Insert(0, savedOrder.Id);
                        _currentIndex = 0;
                    }
                }
                else
                {
                    await _orderService.UpdateOrderAsync(CurrentOrder.Model);
                }
                
                WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
                WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving order");
                ErrorMessage = ex.Message;
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
                
                if (IsNewOrder)
                {
                    var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                    
                    // Update cycling list for next time (even though we are resetting, it keeps the cache fresh)
                    if (_currentIndex == -1)
                    {
                        _allOrderIds.Insert(0, savedOrder.Id);
                    }
                }
                else
                {
                    await _orderService.UpdateOrderAsync(CurrentOrder.Model);
                }

                // Reset to new template
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                if (_authService.CurrentUser?.Branch != null)
                {
                    order.Branch = _authService.CurrentUser.Branch.Value;
                }
                CurrentOrder = new OrderWrapper(order);
                SelectedSupplier = null;
                SelectedProject = null;
                _currentIndex = -1; // Ready for another new order
                IsNewOrder = true;
                
                for (int i = 0; i < 10; i++) AddLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving order");
                ErrorMessage = ex.Message;
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
                if (_authService.CurrentUser?.Branch != null)
                {
                    order.Branch = _authService.CurrentUser.Branch.Value;
                }
                CurrentOrder = new OrderWrapper(order);
                SelectedSupplier = null;
                SelectedProject = null;
                _currentIndex = -1;
                IsNewOrder = true;
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

        public async Task LoadOrderAsync(Guid id)
        {
            OrderId = id;
            await LoadDataAsync();
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
                    IsNewOrder = false;
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
                IsNewOrder = false;
                
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

        private async Task UpdateAddressSuggestionsAsync()
        {
            if (CurrentOrder == null) return;

            if (SelectedAddressSuggestion != null && CurrentOrder.DeliveryAddress == SelectedAddressSuggestion.Description)
                return;

            if (string.IsNullOrWhiteSpace(CurrentOrder.DeliveryAddress) || CurrentOrder.DeliveryAddress.Length < 3)
            {
                AddressSuggestions.Clear();
                return;
            }

            // Ensure Google Maps API key is available
            if (string.IsNullOrEmpty(_connectionSettings.GoogleApiKey))
            {
                var key = await _settingsService.GetGoogleMapsKeyAsync();
                if (!string.IsNullOrEmpty(key))
                {
                    _connectionSettings.GoogleApiKey = key;
                }
            }

            if (string.IsNullOrWhiteSpace(_connectionSettings.GoogleApiKey))
            {
                return;
            }

            // Debounce logic
            _addressCts?.Cancel();
            _addressCts = new System.Threading.CancellationTokenSource();
            var token = _addressCts.Token;

            try
            {
                await Task.Delay(300, token);
                
                var suggestions = await _googleMapsService.GetAddressSuggestionsAsync(CurrentOrder.DeliveryAddress, _addressSessionToken);
                
                if (token.IsCancellationRequested) return;

                AddressSuggestions.Clear();
                foreach (var s in suggestions ?? Array.Empty<AddressSuggestion>())
                {
                    AddressSuggestions.Add(s);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Address Search Error");
            }
        }

        partial void OnSelectedAddressSuggestionChanged(AddressSuggestion? value)
        {
            if (value != null)
            {
                _ = HandleAddressSelectionAsync(value);
            }
        }

        private async Task HandleAddressSelectionAsync(AddressSuggestion suggestion)
        {
            if (suggestion == null || CurrentOrder == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Fetching address details...";
                
                var details = await _googleMapsService.GetPlaceDetailsAsync(suggestion.PlaceId, _addressSessionToken);
                if (details != null)
                {
                    _isHandlingAddressSelection = true;
                    
                    var parts = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrWhiteSpace(details.StreetLine1)) parts.Add(details.StreetLine1);
                    if (!string.IsNullOrWhiteSpace(details.StreetLine2)) parts.Add(details.StreetLine2);
                    if (!string.IsNullOrWhiteSpace(details.City)) parts.Add(details.City);
                    if (!string.IsNullOrWhiteSpace(details.StateOrProvince)) parts.Add(details.StateOrProvince);
                    if (!string.IsNullOrWhiteSpace(details.PostalCode)) parts.Add(details.PostalCode);
                    
                    CurrentOrder.DeliveryAddress = string.Join(", ", parts);
                    
                    AddressSuggestions.Clear();
                    SelectedAddressSuggestion = null;
                    _addressSessionToken = Guid.NewGuid().ToString();
                    _isHandlingAddressSelection = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve address details");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
