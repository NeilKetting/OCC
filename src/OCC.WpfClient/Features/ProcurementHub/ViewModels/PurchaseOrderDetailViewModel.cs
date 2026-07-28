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
        private readonly IDialogService _dialogService;
        private readonly ILogger<PurchaseOrderDetailViewModel> _logger;

        private static readonly Project OtherProjectSentinel = new() { Id = Guid.Empty, Name = "Other..." };
        private bool _isPopulating;

        [ObservableProperty]
        private bool _isOtherProjectSelected;

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
            IDialogService dialogService,
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
            _dialogService = dialogService;
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
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = true);
                }
                else
                {
                    IsBusy = true;
                }
                
                // 1. Load lookups sequentially
                IEnumerable<Project> allProjectsList = new List<Project>();
                if (!Suppliers.Any() || !Projects.Any() || !InventoryItems.Any())
                {
                    IEnumerable<Supplier> suppliers = new List<Supplier>();
                    try
                    {
                        suppliers = await _supplierService.GetSuppliersAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load full suppliers with contacts, falling back to summaries");
                        try
                        {
                            var summaries = await _supplierService.GetSupplierSummariesAsync();
                            suppliers = summaries.Select(s => new Supplier
                            {
                                Id = s.Id,
                                Name = s.Name,
                                Email = s.Email,
                                Phone = s.Phone,
                                ContactPerson = s.ContactPerson,
                                VatNumber = s.VatNumber,
                                Address = s.Address,
                                City = s.City,
                                PostalCode = s.PostalCode,
                                BankName = s.BankName,
                                BankAccountNumber = s.BankAccountNumber,
                                BranchCode = s.BranchCode,
                                SupplierAccountNumber = s.SupplierAccountNumber
                            }).ToList();
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError(ex2, "Failed to load supplier summaries fallback");
                        }
                    }

                    try
                    {
                        allProjectsList = await _projectService.GetProjectsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load projects");
                    }

                    IEnumerable<InventoryItem> inventory = new List<InventoryItem>();
                    try
                    {
                        inventory = await _inventoryService.GetInventoryAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load inventory");
                    }

                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
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
                                var activeProjects = allProjectsList.Where(p => p.Status != "Completed" && p.Status != "Archived" && p.Status != "Cancelled");
                                foreach (var p in activeProjects) Projects.Add(p);
                                Projects.Add(OtherProjectSentinel);
                            }

                            if (!InventoryItems.Any())
                            {
                                InventoryItems.Clear();
                                foreach (var i in inventory) InventoryItems.Add(i);
                            }
                        });
                    }
                    else
                    {
                        if (!Suppliers.Any())
                        {
                            Suppliers.Clear();
                            foreach (var s in suppliers) Suppliers.Add(s);
                        }

                        if (!Projects.Any())
                        {
                            Projects.Clear();
                            var activeProjects = allProjectsList.Where(p => p.Status != "Completed" && p.Status != "Archived" && p.Status != "Cancelled");
                            foreach (var p in activeProjects) Projects.Add(p);
                            Projects.Add(OtherProjectSentinel);
                        }

                        if (!InventoryItems.Any())
                        {
                            InventoryItems.Clear();
                            foreach (var i in inventory) InventoryItems.Add(i);
                        }
                    }
                }
                else
                {
                    allProjectsList = await _projectService.GetProjectsAsync();
                }

                // 2. Fetch all existing order IDs for cycling (newest first)
                if (_allOrderIds == null || !_allOrderIds.Any())
                {
                    var allOrders = await _orderService.GetOrdersAsync();
                    _allOrderIds = allOrders.OrderByDescending(o => o.OrderDate).Select(o => o.Id).ToList();
                }

                // 3. Populate or create order
                _isPopulating = true;
                try
                {
                    if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                    {
                        // Load existing order
                        var order = await _orderService.GetOrderAsync(OrderId.Value);
                        if (order != null)
                        {
                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                                {
                                    CurrentOrder = new OrderWrapper(order);
                                    ResolveSupplierSelection(order.SupplierId, order.SupplierName);
                                    await ResolveProjectSelectionAsync(order.ProjectId, order.ProjectName, allProjectsList);
                                    _currentIndex = _allOrderIds.IndexOf(order.Id);
                                    IsNewOrder = false;
                                });
                            }
                            else
                            {
                                CurrentOrder = new OrderWrapper(order);
                                ResolveSupplierSelection(order.SupplierId, order.SupplierName);
                                await ResolveProjectSelectionAsync(order.ProjectId, order.ProjectName, allProjectsList);
                                _currentIndex = _allOrderIds.IndexOf(order.Id);
                                IsNewOrder = false;
                            }
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

                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                CurrentOrder = new OrderWrapper(order);
                                _currentIndex = -1; // -1 represents "New Order"
                                IsNewOrder = true;
                                SelectedProject = null;
                                SelectedSupplier = null;
                                
                                for (int i = 0; i < 10; i++)
                                {
                                    AddLine();
                                }
                            });
                        }
                        else
                        {
                            CurrentOrder = new OrderWrapper(order);
                            _currentIndex = -1;
                            IsNewOrder = true;
                            SelectedProject = null;
                            SelectedSupplier = null;

                            for (int i = 0; i < 10; i++)
                            {
                                AddLine();
                            }
                        }
                    }
                    else
                    {
                        // If CurrentOrder is already present, synchronize dropdowns
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                if (SelectedSupplier == null && (CurrentOrder.SupplierId != Guid.Empty || !string.IsNullOrEmpty(CurrentOrder.SupplierName)))
                                {
                                    ResolveSupplierSelection(CurrentOrder.SupplierId, CurrentOrder.SupplierName);
                                }
                                if (SelectedProject == null && ((CurrentOrder.ProjectId.HasValue && CurrentOrder.ProjectId.Value != Guid.Empty) || !string.IsNullOrEmpty(CurrentOrder.ProjectName)))
                                {
                                    await ResolveProjectSelectionAsync(CurrentOrder.ProjectId, CurrentOrder.ProjectName, allProjectsList);
                                }
                            });
                        }
                        else
                        {
                            if (SelectedSupplier == null && (CurrentOrder.SupplierId != Guid.Empty || !string.IsNullOrEmpty(CurrentOrder.SupplierName)))
                            {
                                ResolveSupplierSelection(CurrentOrder.SupplierId, CurrentOrder.SupplierName);
                            }
                            if (SelectedProject == null && ((CurrentOrder.ProjectId.HasValue && CurrentOrder.ProjectId.Value != Guid.Empty) || !string.IsNullOrEmpty(CurrentOrder.ProjectName)))
                            {
                                await ResolveProjectSelectionAsync(CurrentOrder.ProjectId, CurrentOrder.ProjectName, allProjectsList);
                            }
                        }
                    }
                }
                finally
                {
                    _isPopulating = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase order details data");
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        ErrorMessage = "Failed to load required data. Please try again.");
                }
                else
                {
                    ErrorMessage = "Failed to load required data. Please try again.";
                }
            }
            finally
            {
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsBusy = false);
                }
                else
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ResolveProjectSelectionAsync(Guid? projectId, string? projectName, IEnumerable<Project>? allProjectsList = null)
        {
            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                var matchingProject = Projects.FirstOrDefault(p => p.Id == projectId.Value);
                if (matchingProject == null)
                {
                    var all = allProjectsList ?? await _projectService.GetProjectsAsync();
                    var originalProject = all.FirstOrDefault(p => p.Id == projectId.Value);
                    if (originalProject != null)
                    {
                        int otherIndex = Projects.IndexOf(OtherProjectSentinel);
                        if (otherIndex >= 0) Projects.Insert(otherIndex, originalProject);
                        else Projects.Add(originalProject);
                        matchingProject = originalProject;
                    }
                }
                SelectedProject = matchingProject;
            }
            else if (!string.IsNullOrEmpty(projectName))
            {
                SelectedProject = OtherProjectSentinel;
            }
            else
            {
                SelectedProject = null;
            }
        }

        private void ResolveSupplierSelection(Guid? supplierId, string? supplierName)
        {
            Supplier? matchingSupplier = null;

            if (supplierId.HasValue && supplierId.Value != Guid.Empty)
            {
                matchingSupplier = Suppliers.FirstOrDefault(s => s.Id == supplierId.Value);
            }

            if (matchingSupplier == null && !string.IsNullOrWhiteSpace(supplierName))
            {
                matchingSupplier = Suppliers.FirstOrDefault(s => string.Equals(s.Name, supplierName, StringComparison.OrdinalIgnoreCase));
            }

            if (matchingSupplier == null && !string.IsNullOrWhiteSpace(supplierName))
            {
                matchingSupplier = Suppliers.FirstOrDefault(s => s.Name.Contains(supplierName, StringComparison.OrdinalIgnoreCase) || supplierName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (matchingSupplier == null && (!string.IsNullOrWhiteSpace(supplierName) || (supplierId.HasValue && supplierId.Value != Guid.Empty)))
            {
                matchingSupplier = new Supplier
                {
                    Id = (supplierId.HasValue && supplierId.Value != Guid.Empty) ? supplierId.Value : Guid.NewGuid(),
                    Name = !string.IsNullOrWhiteSpace(supplierName) ? supplierName : "Unknown Supplier"
                };
                Suppliers.Add(matchingSupplier);
            }

            SelectedSupplier = matchingSupplier;
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (_isPopulating) return;

            if (value != null && CurrentOrder != null)
            {
                CurrentOrder.SupplierId = value.Id;
                CurrentOrder.SupplierName = value.Name;
                CurrentOrder.EntityAddress = value.Address;
                CurrentOrder.EntityTel = value.Phone;
                CurrentOrder.EntityVatNo = value.VatNumber;
            }

            if (value != null && value.Id != Guid.Empty && (value.Contacts == null || !value.Contacts.Any()))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var freshSupplier = await _supplierService.GetSupplierAsync(value.Id);
                        if (freshSupplier != null && freshSupplier.Contacts != null && freshSupplier.Contacts.Any())
                        {
                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (SelectedSupplier?.Id == freshSupplier.Id)
                                    {
                                        SelectedSupplier = freshSupplier;
                                        var idx = Suppliers.IndexOf(Suppliers.FirstOrDefault(s => s.Id == freshSupplier.Id));
                                        if (idx >= 0) Suppliers[idx] = freshSupplier;
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to asynchronously load supplier contacts for {SupplierId}", value.Id);
                    }
                });
            }
        }

        partial void OnSelectedProjectChanged(Project? value)
        {
            if (CurrentOrder == null) return;

            if (value != null)
            {
                if (value.Id == Guid.Empty) // "Other..." sentinel
                {
                    CurrentOrder.ProjectId = null;
                    IsOtherProjectSelected = true;
                    if (CurrentOrder.ProjectName == null || CurrentOrder.ProjectName == "Other...")
                    {
                        CurrentOrder.ProjectName = string.Empty;
                    }
                }
                else
                {
                    CurrentOrder.ProjectId = value.Id;
                    CurrentOrder.ProjectName = value.Name;
                    CurrentOrder.Attention = value.ProjectManager ?? string.Empty;
                    IsOtherProjectSelected = false;

                    if (!_isPopulating)
                    {
                        // Automatically select "Site" destination type for delivery address on interactive user selection
                        CurrentOrder.DestinationType = OrderDestinationType.Site;
                    }
                }
            }
            else
            {
                CurrentOrder.ProjectId = null;
                CurrentOrder.ProjectName = null;
                IsOtherProjectSelected = false;
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

        private bool ValidateAndPrepareOrderForSave()
        {
            if (CurrentOrder == null) return false;

            // 1. Check Supplier
            if (SelectedSupplier == null && (CurrentOrder.SupplierId == null || CurrentOrder.SupplierId == Guid.Empty) && string.IsNullOrWhiteSpace(CurrentOrder.SupplierName))
            {
                _toastService.ShowError("Save Failed", "Please select a supplier for the purchase order.");
                return false;
            }

            // 2. Resolve missing InventoryItemIds for typed SKUs
            foreach (var line in CurrentOrder.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.ItemCode) && (!line.InventoryItemId.HasValue || line.InventoryItemId.Value == Guid.Empty))
                {
                    var match = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        line.InventoryItemId = match.Id;
                    }
                }
            }

            // 3. Remove completely blank placeholder lines (empty item code and description)
            var invalidLines = CurrentOrder.Lines
                .Where(l => string.IsNullOrWhiteSpace(l.ItemCode) && string.IsNullOrWhiteSpace(l.Description))
                .ToList();
            foreach (var line in invalidLines)
            {
                CurrentOrder.Lines.Remove(line);
            }

            // 4. Ensure at least 1 valid line item exists
            if (!CurrentOrder.Lines.Any())
            {
                _toastService.ShowError("Save Failed", "Please add at least one line item to the purchase order.");
                return false;
            }

            // 5. Ensure ETA is not in the past
            if (CurrentOrder.ExpectedDeliveryDate.HasValue && CurrentOrder.ExpectedDeliveryDate.Value.Date < DateTime.Today)
            {
                CurrentOrder.ExpectedDeliveryDate = DateTime.Today.AddDays(7);
            }

            return true;
        }

        /// <summary>
        /// Saves the purchase order without closing the detail view.
        /// </summary>
        /// <param name="showToast">Whether to display a success toast notification.</param>
        /// <returns>True if the order was successfully saved; otherwise false.</returns>
        private async Task<bool> SaveOrderWithoutClosingAsync(bool showToast = true)
        {
            if (CurrentOrder == null) return false;
            if (!ValidateAndPrepareOrderForSave()) return false;

            try
            {
                IsBusy = true;
                BusyText = "Saving order...";

                if (IsNewOrder)
                {
                    var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                    CurrentOrder = new OrderWrapper(savedOrder);
                    OrderId = savedOrder.Id;
                    IsNewOrder = false;

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

                if (showToast)
                {
                    _toastService.ShowSuccess("Order Saved", $"Purchase Order {CurrentOrder.OrderNumber} saved successfully.");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving order");
                ErrorMessage = ex.Message;
                _toastService.ShowError("Save Error", ex.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
                BusyText = string.Empty;
            }
        }

        /// <summary>
        /// Saves the purchase order and closes the view.
        /// </summary>
        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            bool saved = await SaveOrderWithoutClosingAsync(showToast: true);
            if (saved)
            {
                WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
                WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            }
        }

        /// <summary>
        /// Saves the purchase order and resets to a blank order template.
        /// </summary>
        [RelayCommand]
        private async Task SaveAndNewAsync()
        {
            bool saved = await SaveOrderWithoutClosingAsync(showToast: true);
            if (!saved) return;

            try
            {
                IsBusy = true;

                // Reset to new template
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                if (_authService.CurrentUser?.Branch != null)
                {
                    order.Branch = _authService.CurrentUser.Branch.Value;
                }
                _isPopulating = true;
                try
                {
                    CurrentOrder = new OrderWrapper(order);
                    SelectedSupplier = null;
                    SelectedProject = null;
                    _currentIndex = -1; // Ready for another new order
                    IsNewOrder = true;
                }
                finally
                {
                    _isPopulating = false;
                }
                
                for (int i = 0; i < 10; i++) AddLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting order after save");
                ErrorMessage = ex.Message;
                _toastService.ShowError("Save Error", ex.Message);
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
                _isPopulating = true;
                try
                {
                    CurrentOrder = new OrderWrapper(order);
                    SelectedSupplier = null;
                    SelectedProject = null;
                    _currentIndex = -1;
                    IsNewOrder = true;
                }
                finally
                {
                    _isPopulating = false;
                }
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

        private void CleanupEmptyLines()
        {
            if (CurrentOrder == null) return;
            var emptyLines = CurrentOrder.Lines
                .Where(l => string.IsNullOrWhiteSpace(l.ItemCode) && string.IsNullOrWhiteSpace(l.Description) && l.QuantityOrdered == 0 && l.UnitPrice == 0)
                .ToList();
            foreach (var line in emptyLines)
            {
                CurrentOrder.Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void UpdateLineItem(OrderLineWrapper line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.ItemCode)) return;

            // Real-time update: Only update if we find a match. DO NOT show popup while typing.
            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                bool isNewItemForLine = line.InventoryItemId != item.Id;

                line.InventoryItemId = item.Id;
                if (isNewItemForLine || string.IsNullOrWhiteSpace(line.Description))
                {
                    line.Description = item.Description;
                }
                if (isNewItemForLine || string.IsNullOrWhiteSpace(line.UnitOfMeasure))
                {
                    line.UnitOfMeasure = item.UnitOfMeasure;
                }
                if (isNewItemForLine || line.UnitPrice == 0)
                {
                    line.UnitPrice = item.AverageCost;
                }
                line.UpdateCalculations();
            }
        }

        [RelayCommand]
        private void ValidateLineItem(OrderLineWrapper line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.ItemCode)) return;

            // FIX: If we just showed a dialog for THIS EXACT CODE and the user said No, don't nag them again.
            if (line.ItemCode.Equals(line.LastValidatedSku, StringComparison.OrdinalIgnoreCase)) return;

            // Final validation: If item not found, show the dialog.
            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.LastValidatedSku = line.ItemCode; // Mark as validated
                bool isNewItemForLine = line.InventoryItemId != item.Id;

                line.InventoryItemId = item.Id;
                if (isNewItemForLine || string.IsNullOrWhiteSpace(line.Description))
                {
                    line.Description = item.Description;
                }
                if (isNewItemForLine || string.IsNullOrWhiteSpace(line.UnitOfMeasure))
                {
                    line.UnitOfMeasure = item.UnitOfMeasure;
                }
                if (isNewItemForLine || line.UnitPrice == 0)
                {
                    line.UnitPrice = item.AverageCost;
                }
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
                    _isPopulating = true;
                    try
                    {
                        CurrentOrder = new OrderWrapper(order);
                        ResolveSupplierSelection(order.SupplierId, order.SupplierName);
                        await ResolveProjectSelectionAsync(order.ProjectId, order.ProjectName);
                        IsNewOrder = false;
                    }
                    finally
                    {
                        _isPopulating = false;
                    }
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
            dialog.OrderSelected += async (order) =>
            {
                _isPopulating = true;
                try
                {
                    CurrentOrder = new OrderWrapper(order);
                    SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == order.SupplierId);
                    await ResolveProjectSelectionAsync(order.ProjectId, order.ProjectName);
                    
                    // Update index for cycling
                    _currentIndex = _allOrderIds.IndexOf(order.Id);
                    IsNewOrder = false;
                }
                finally
                {
                    _isPopulating = false;
                }
                
                CloseOverlay();
            };
            OpenOverlay(dialog);
        }

        /// <summary>
        /// Saves the purchase order first, then generates and opens the PDF preview.
        /// </summary>
        [RelayCommand]
        private async Task PreviewOrderAsync()
        {
            if (CurrentOrder == null) return;
            if (!await SaveOrderWithoutClosingAsync(showToast: true)) return;

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

        /// <summary>
        /// Saves the purchase order first, then triggers PDF generation for printing.
        /// </summary>
        [RelayCommand]
        private async Task PrintOrderAsync()
        {
            // For now, same as preview (User can print from PDF viewer)
            await PreviewOrderAsync();
        }

        /// <summary>
        /// Saves the purchase order first, then generates the PDF and opens the email client.
        /// </summary>
        [RelayCommand]
        private async Task EmailOrderAsync()
        {
            if (CurrentOrder == null) return;
            if (!await SaveOrderWithoutClosingAsync(showToast: true)) return;

            try
            {
                IsBusy = true;
                BusyText = "Preparing email...";
                var path = await _pdfService.GenerateOrderPdfAsync(CurrentOrder.Model);

                var emails = new List<string>();
                if (SelectedSupplier != null)
                {
                    if (SelectedSupplier.Id != Guid.Empty)
                    {
                        try
                        {
                            var freshSupplier = await _supplierService.GetSupplierAsync(SelectedSupplier.Id);
                            if (freshSupplier != null)
                            {
                                SelectedSupplier = freshSupplier;
                                var idx = Suppliers.IndexOf(Suppliers.FirstOrDefault(s => s.Id == freshSupplier.Id));
                                if (idx >= 0) Suppliers[idx] = freshSupplier;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not refresh supplier details for {SupplierId}", SelectedSupplier.Id);
                        }
                    }

                    var mainEmails = EmailHelper.ParseEmailAddresses(SelectedSupplier.Email);
                    foreach (var e in mainEmails)
                    {
                        if (!emails.Contains(e, StringComparer.OrdinalIgnoreCase)) emails.Add(e);
                    }

                    if (SelectedSupplier.Contacts != null)
                    {
                        foreach (var contact in SelectedSupplier.Contacts)
                        {
                            if (!string.IsNullOrWhiteSpace(contact.Email))
                            {
                                var contactEmails = EmailHelper.ParseEmailAddresses(contact.Email);
                                foreach (var ce in contactEmails)
                                {
                                    if (!emails.Contains(ce, StringComparer.OrdinalIgnoreCase)) emails.Add(ce);
                                }
                            }
                        }
                    }
                }

                string recipientEmail = string.Empty;

                if (emails.Count > 1)
                {
                    IsBusy = false;
                    var tcs = new TaskCompletionSource<string?>();
                    var dialog = new SelectEmailViewModel(SelectedSupplier?.Name ?? "Supplier", emails);

                    dialog.AddContactRequested += (callback) =>
                    {
                        var contactDialog = new AddSupplierContactViewModel(SelectedSupplier?.Name ?? "Supplier");
                        contactDialog.Completed += async (newContact) =>
                        {
                            CloseOverlay();
                            if (newContact != null && SelectedSupplier != null)
                            {
                                newContact.SupplierId = SelectedSupplier.Id;
                                SelectedSupplier.Contacts ??= new List<SupplierContact>();
                                SelectedSupplier.Contacts.Add(newContact);

                                if (string.IsNullOrWhiteSpace(SelectedSupplier.Email))
                                {
                                    SelectedSupplier.Email = newContact.Email;
                                }
                                if (string.IsNullOrWhiteSpace(SelectedSupplier.ContactPerson))
                                {
                                    SelectedSupplier.ContactPerson = newContact.ContactName;
                                }

                                try
                                {
                                    await _supplierService.UpdateSupplierAsync(SelectedSupplier);
                                    _toastService.ShowSuccess("Supplier Contact Saved", $"Saved '{newContact.ContactName}' ({newContact.Email}) to supplier {SelectedSupplier.Name}.");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Could not save supplier contact automatically");
                                }
                            }
                            OpenOverlay(dialog);
                            callback(newContact);
                        };
                        OpenOverlay(contactDialog);
                    };

                    dialog.Completed += (selected) =>
                    {
                        CloseOverlay();
                        tcs.TrySetResult(selected);
                    };
                    OpenOverlay(dialog);
                    var userChosen = await tcs.Task;
                    if (string.IsNullOrWhiteSpace(userChosen))
                    {
                        return; // User cancelled
                    }
                    recipientEmail = userChosen;
                }
                else if (emails.Count == 1)
                {
                    recipientEmail = emails[0];
                }
                else
                {
                    IsBusy = false;
                    // No email on file - prompt user with custom contact overlay dialog to save contact to supplier
                    var tcs = new TaskCompletionSource<SupplierContact?>();
                    var dialog = new AddSupplierContactViewModel(SelectedSupplier?.Name ?? "Supplier");
                    dialog.Completed += (newContact) =>
                    {
                        CloseOverlay();
                        tcs.TrySetResult(newContact);
                    };
                    OpenOverlay(dialog);
                    var newContact = await tcs.Task;
                    if (newContact == null || string.IsNullOrWhiteSpace(newContact.Email))
                    {
                        return; // Cancelled
                    }

                    recipientEmail = newContact.Email.Trim();

                    // Persist new contact information to the supplier
                    if (SelectedSupplier != null)
                    {
                        newContact.SupplierId = SelectedSupplier.Id;
                        SelectedSupplier.Contacts ??= new List<SupplierContact>();
                        SelectedSupplier.Contacts.Add(newContact);

                        if (string.IsNullOrWhiteSpace(SelectedSupplier.Email))
                        {
                            SelectedSupplier.Email = newContact.Email;
                        }
                        if (string.IsNullOrWhiteSpace(SelectedSupplier.ContactPerson))
                        {
                            SelectedSupplier.ContactPerson = newContact.ContactName;
                        }

                        try
                        {
                            await _supplierService.UpdateSupplierAsync(SelectedSupplier);
                            _toastService.ShowSuccess("Supplier Contact Saved", $"Saved '{newContact.ContactName}' ({newContact.Email}) to supplier {SelectedSupplier.Name}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not save supplier contact automatically");
                        }
                    }
                }

                IsBusy = true;
                BusyText = "Opening email client...";

                var contactPerson = SelectedSupplier?.ContactPerson;
                if (string.IsNullOrWhiteSpace(contactPerson)) contactPerson = SelectedSupplier?.Name ?? "Supplier";

                var subject = $"Purchase Order {CurrentOrder.OrderNumber} - Orange Circle Construction (Pty) Ltd";
                var body = $"Dear {contactPerson},\n\nPlease find attached Purchase Order {CurrentOrder.OrderNumber}.\n\nKind regards,\nOrange Circle Construction (Pty) Ltd";

                bool usedOutlook = EmailHelper.OpenEmailWithAttachment(recipientEmail, subject, body, path);

                if (usedOutlook)
                {
                    _toastService.ShowSuccess("Email Created", $"Outlook opened with Purchase Order {CurrentOrder.OrderNumber} attached for {recipientEmail}.");
                }
                else
                {
                    _toastService.ShowInfo("Email Prepared", $"Default mail client opened for {recipientEmail}. PDF location opened in File Explorer.");
                }
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

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AddressSuggestions.Clear();
                    foreach (var s in suggestions ?? Array.Empty<AddressSuggestion>())
                    {
                        AddressSuggestions.Add(s);
                    }
                });
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
