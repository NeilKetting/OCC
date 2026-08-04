using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Features.ProcurementHub.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    /// <summary>
    /// ViewModel for the Stock Picking / Project Allocation order screen.
    /// Manages creation and editing of picking orders (issuing inventory to project sites).
    /// Race conditions fixed by mirroring the same sequential-phase load pattern as
    /// <see cref="PurchaseOrderDetailViewModel"/>:
    ///   1. Fetch lookups (projects + inventory) in parallel on background thread.
    ///   2. Set collections on UI thread — all sync, no await inside.
    ///   3. Fetch order / create template.
    ///   4. Resolve project selection — AFTER order is set, and guarded by _isPopulating.
    /// </summary>
    public partial class PickingOrderViewModel : OverlayHostViewModel
    {
        // ─── Services ─────────────────────────────────────────────────────────────

        private readonly IOrderService _orderService;
        private readonly IProjectService _projectService;
        private readonly IInventoryService _inventoryService;
        private readonly InventoryCacheService _inventoryCache;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ISupplierService _supplierService;
        private readonly IPdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<PickingOrderViewModel> _logger;

        // ─── State Guards ──────────────────────────────────────────────────────────

        /// <summary>
        /// Set to true during programmatic population of the order so that
        /// <see cref="OnSelectedProjectChanged"/> does not overwrite just-set fields.
        /// Released only AFTER all async resolution is complete.
        /// </summary>
        private bool _isPopulating;

        /// <summary>
        /// SemaphoreSlim re-entrancy guard for <see cref="LoadDataAsync"/>.
        /// Prevents concurrent loads from racing (e.g., SignalR update + navigation).
        /// </summary>
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

        private bool _isNewOrder = true;

        // ─── Observable Properties ────────────────────────────────────────────────

        [ObservableProperty]
        private OrderWrapper? _currentOrder;

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private ObservableCollection<InventoryItem> _inventoryItems = new();

        [ObservableProperty]
        private Project? _selectedProject;

        [ObservableProperty]
        private Guid? _orderId;

        private List<Guid> _allOrderIds = new();
        private int _currentIndex = -1;

        // ─── Constructor ──────────────────────────────────────────────────────────

        public PickingOrderViewModel(
            IOrderService orderService,
            IProjectService projectService,
            IInventoryService inventoryService,
            InventoryCacheService inventoryCache,
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
            _inventoryCache = inventoryCache;
            _authService = authService;
            _toastService = toastService;
            _supplierService = supplierService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _logger = logger;

            Title = "Stock Picking & Project Allocation";
        }

        // ─── Main Load Command ────────────────────────────────────────────────────

        /// <summary>
        /// Entry point for loading the screen. Follows the same sequential-phase pattern
        /// as <see cref="PurchaseOrderDetailViewModel.LoadDataAsync"/> to eliminate the
        /// Dispatcher.InvokeAsync(async lambda) race conditions.
        /// </summary>
        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (!await _loadSemaphore.WaitAsync(0))
            {
                _logger.LogDebug("PickingOrderViewModel.LoadDataAsync skipped — already running.");
                return;
            }

            try
            {
                IsBusy = true;

                // ── Phase 1: Fetch projects and inventory in parallel (background). ──
                var projectsTask = LoadProjectsAsync();
                var inventoryTask = LoadInventoryAsync();

                await Task.WhenAll(projectsTask, inventoryTask);

                var projects = projectsTask.Result;
                var inventory = inventoryTask.Result;

                // ── Phase 2: Assign collections synchronously on the calling thread. ──
                // No awaits inside — eliminates the Dispatcher.InvokeAsync(async lambda) race.
                SetLookupCollections(projects, inventory);

                // ── Phase 3: Fetch picking order cycling list. ──
                if (!_allOrderIds.Any())
                {
                    var allOrders = await _orderService.GetOrdersAsync();
                    _allOrderIds = allOrders
                        .Where(o => o.OrderType == OrderType.PickingOrder)
                        .OrderByDescending(o => o.OrderDate)
                        .Select(o => o.Id)
                        .ToList();
                }

                // ── Phase 4: Populate existing or create new — fully guarded by _isPopulating. ──
                _isPopulating = true;
                try
                {
                    if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                    {
                        await PopulateExistingOrderAsync(OrderId.Value);
                    }
                    else if (CurrentOrder == null)
                    {
                        await PopulateNewOrderAsync();
                    }
                    else
                    {
                        // Order already loaded (e.g., SignalR update) — just re-resolve project
                        ResolveProjectSelection(CurrentOrder.ProjectId);
                    }
                }
                finally
                {
                    // Release _isPopulating only AFTER all async resolution is complete.
                    _isPopulating = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading picking order details data");
                ErrorMessage = "Failed to load required data. Please try again.";
            }
            finally
            {
                IsBusy = false;
                _loadSemaphore.Release();
            }
        }

        private async Task<IEnumerable<Project>> LoadProjectsAsync()
        {
            try
            {
                return await _projectService.GetProjectsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load projects for picking order");
                return Enumerable.Empty<Project>();
            }
        }

        private async Task<IEnumerable<InventoryItem>> LoadInventoryAsync()
        {
            try
            {
                // Use the shared TTL cache (5-minute window) rather than always-fetching.
                // For picking orders, we also filter by branch stock level so only items
                // with available quantity are shown in the SKU dropdown.
                var all = await _inventoryCache.GetAsync();
                var branch = _authService.CurrentUser?.Branch ?? Branch.JHB;
                return all.Where(i =>
                    (branch == Branch.JHB && i.JhbQuantity > 0) ||
                    (branch == Branch.CPT && i.CptQuantity > 0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load inventory for picking order");
                return Enumerable.Empty<InventoryItem>();
            }
        }

        /// <summary>
        /// Sets the project and inventory observable collections.
        /// Purely synchronous — no awaits — so it runs cleanly without any async-lambda pitfalls.
        /// </summary>
        private void SetLookupCollections(IEnumerable<Project> projects, IEnumerable<InventoryItem> inventory)
        {
            Projects.Clear();
            foreach (var p in projects) Projects.Add(p);

            InventoryItems.Clear();
            foreach (var i in inventory) InventoryItems.Add(i);

            _logger.LogDebug(
                "Picking order lookup collections set: {ProjectCount} projects, {ItemCount} inventory items",
                Projects.Count, InventoryItems.Count);
        }

        /// <summary>
        /// Loads an existing picking order and resolves the project selection.
        /// Caller must hold <c>_isPopulating = true</c>.
        /// </summary>
        private async Task PopulateExistingOrderAsync(Guid orderId)
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("GetOrderAsync returned null for picking order {OrderId}", orderId);
                return;
            }

            CurrentOrder = new OrderWrapper(order);
            _currentIndex = _allOrderIds.IndexOf(order.Id);
            _isNewOrder = false;

            // Resolve project AFTER CurrentOrder is set, and within the _isPopulating guard.
            ResolveProjectSelection(order.ProjectId);

            _logger.LogInformation(
                "Populated existing picking order {OrderNumber} with {LineCount} lines",
                order.OrderNumber, order.Lines.Count);
        }

        /// <summary>
        /// Creates a new blank picking order template and pre-populates 10 empty rows.
        /// Caller must hold <c>_isPopulating = true</c>.
        /// </summary>
        private async Task PopulateNewOrderAsync()
        {
            var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PickingOrder);
            order.DestinationType = OrderDestinationType.Site;
            if (_authService.CurrentUser?.Branch != null)
                order.Branch = _authService.CurrentUser.Branch.Value;

            CurrentOrder = new OrderWrapper(order);
            SelectedProject = null;
            _currentIndex = -1;
            _isNewOrder = true;

            for (int i = 0; i < 10; i++) AddLine();
        }

        // ─── Selection Resolver ───────────────────────────────────────────────────

        /// <summary>
        /// Resolves <see cref="SelectedProject"/> from a project ID.
        /// Silently no-ops if <paramref name="projectId"/> is null or not found.
        /// </summary>
        private void ResolveProjectSelection(Guid? projectId)
        {
            if (!projectId.HasValue || projectId.Value == Guid.Empty)
            {
                SelectedProject = null;
                return;
            }

            SelectedProject = Projects.FirstOrDefault(p => p.Id == projectId.Value);
            _logger.LogInformation(
                "ResolveProjectSelection: {Name} ({Id})",
                SelectedProject?.Name, SelectedProject?.Id);
        }

        partial void OnSelectedProjectChanged(Project? value)
        {
            // Guard: do not overwrite order fields while loading
            if (_isPopulating) return;

            if (value != null && CurrentOrder != null)
            {
                CurrentOrder.ProjectId = value.Id;
                CurrentOrder.ProjectName = value.Name;
                CurrentOrder.Attention = value.ProjectManager ?? string.Empty;
            }
        }

        // ─── Line Management ──────────────────────────────────────────────────────

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
        private void UpdateLineItem(OrderLineWrapper line)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) return;

            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.InventoryItemId = item.Id;
                line.Description = item.Description;
                line.UnitOfMeasure = item.UnitOfMeasure;
                line.UnitPrice = 0; // Picking orders have no price
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

        // ─── Save ─────────────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            if (CurrentOrder == null) return;

            if (SelectedProject == null)
            {
                _toastService.ShowError("Save Failed", "Please select a Project/Site for the picking order.");
                return;
            }

            var validLines = CurrentOrder.Lines.Where(l => l.IsItemValid && l.QuantityOrdered > 0).ToList();
            if (!validLines.Any())
            {
                _toastService.ShowError("Save Failed", "Please add at least one item with a valid pick quantity.");
                return;
            }

            try
            {
                IsBusy = true;

                // Rebuild model.Lines from only valid wrapper lines
                CurrentOrder.Model.Lines.Clear();
                foreach (var line in validLines)
                {
                    line.UnitPrice = 0; // Picking orders carry no price
                    CurrentOrder.Model.Lines.Add(line.Model);
                }

                if (_isNewOrder)
                {
                    var savedOrder = await _orderService.CreateOrderAsync(CurrentOrder.Model);
                    _toastService.ShowSuccess("Success", "Picking order created successfully.");

                    if (_currentIndex == -1)
                    {
                        _allOrderIds.Insert(0, savedOrder.Id);
                        _currentIndex = 0;
                    }
                }
                else
                {
                    await _orderService.UpdateOrderAsync(CurrentOrder.Model);
                    _toastService.ShowSuccess("Success", "Picking order updated successfully.");
                }

                WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
                WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving picking order");
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ─── Clear / Cancel ───────────────────────────────────────────────────────

        [RelayCommand]
        private async Task ClearOrderAsync()
        {
            try
            {
                IsBusy = true;
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PickingOrder);
                order.DestinationType = OrderDestinationType.Site;
                if (_authService.CurrentUser?.Branch != null)
                    order.Branch = _authService.CurrentUser.Branch.Value;

                _isPopulating = true;
                try
                {
                    CurrentOrder = new OrderWrapper(order);
                    SelectedProject = null;
                    _currentIndex = -1;
                    _isNewOrder = true;
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
        private void Cancel()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }

        // ─── Navigation (Prev / Next) ─────────────────────────────────────────────

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

        /// <summary>External entry point used by the Procurement list to open a specific order.</summary>
        public async Task LoadOrderAsync(Guid id)
        {
            OrderId = id;
            await LoadDataAsync();
        }

        /// <summary>
        /// Loads a picking order by ID for prev/next cycling.
        /// Wraps assignment in _isPopulating guard to prevent OnSelectedProjectChanged
        /// from firing mid-assignment.
        /// </summary>
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
                        ResolveProjectSelection(order.ProjectId);
                        _isNewOrder = false;
                    }
                    finally
                    {
                        _isPopulating = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading picking order {Id}", id);
                ErrorMessage = "Failed to load picking order.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ─── Find Order Dialog ─────────────────────────────────────────────────────

        [RelayCommand]
        private void FindOrder()
        {
            var dialog = new FindOrderViewModel(_orderService, _supplierService, "Picking Order");
            dialog.CloseRequested += CloseOverlay;

            // Use a named handler (not anonymous lambda) to avoid the async fire-and-forget pattern.
            dialog.OrderSelected += OnOrderSelectedAsync;

            OpenOverlay(dialog);
        }

        /// <summary>
        /// Handles the order selection event from the FindOrder dialog.
        /// Named method (not anonymous lambda) so the async work is properly awaited
        /// rather than fire-and-forgot.
        /// </summary>
        private void OnOrderSelectedAsync(Order order)
        {
            _isPopulating = true;
            try
            {
                CurrentOrder = new OrderWrapper(order);
                ResolveProjectSelection(order.ProjectId);
                _currentIndex = _allOrderIds.IndexOf(order.Id);
                _isNewOrder = false;
            }
            finally
            {
                _isPopulating = false;
            }

            CloseOverlay();
        }

        // ─── PDF / Print / Email ──────────────────────────────────────────────────

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

                Supplier? supplier = CurrentOrder.SupplierId.HasValue
                    ? await _supplierService.GetSupplierAsync(CurrentOrder.SupplierId.Value)
                    : null;
                var supplierName = supplier?.Name ?? CurrentOrder.SupplierName ?? "Supplier";
                var emails = new List<string>();
                if (supplier != null)
                {
                    var mainEmails = EmailHelper.ParseEmailAddresses(supplier.Email);
                    foreach (var e in mainEmails)
                        if (!emails.Contains(e, StringComparer.OrdinalIgnoreCase)) emails.Add(e);

                    if (supplier.Contacts != null)
                    {
                        foreach (var contact in supplier.Contacts)
                        {
                            if (!string.IsNullOrWhiteSpace(contact.Email))
                            {
                                var contactEmails = EmailHelper.ParseEmailAddresses(contact.Email);
                                foreach (var ce in contactEmails)
                                    if (!emails.Contains(ce, StringComparer.OrdinalIgnoreCase)) emails.Add(ce);
                            }
                        }
                    }
                }

                string recipientEmail = string.Empty;

                if (emails.Count > 1)
                {
                    IsBusy = false;
                    var tcs = new TaskCompletionSource<string?>();
                    var dialog = new SelectEmailViewModel(supplierName, emails);

                    dialog.AddContactRequested += (callback) =>
                    {
                        var contactDialog = new AddSupplierContactViewModel(supplierName);
                        contactDialog.Completed += async (newContact) =>
                        {
                            CloseOverlay();
                            if (newContact != null && supplier != null)
                            {
                                newContact.SupplierId = supplier.Id;
                                supplier.Contacts ??= new List<SupplierContact>();
                                supplier.Contacts.Add(newContact);

                                if (string.IsNullOrWhiteSpace(supplier.Email))
                                    supplier.Email = newContact.Email;
                                if (string.IsNullOrWhiteSpace(supplier.ContactPerson))
                                    supplier.ContactPerson = newContact.ContactName;

                                try
                                {
                                    await _supplierService.UpdateSupplierAsync(supplier);
                                    _toastService.ShowSuccess("Supplier Contact Saved", $"Saved '{newContact.ContactName}' ({newContact.Email}) to supplier {supplier.Name}.");
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
                    if (string.IsNullOrWhiteSpace(userChosen)) return;
                    recipientEmail = userChosen;
                }
                else if (emails.Count == 1)
                {
                    recipientEmail = emails[0];
                }
                else
                {
                    IsBusy = false;
                    var entered = await _dialogService.ShowInputDialogAsync(
                        "Recipient Email", "Enter recipient email address:", supplier?.Email ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(entered)) return;
                    recipientEmail = entered.Trim();
                }

                IsBusy = true;
                BusyText = "Opening email client...";

                var subject = $"Picking Order {CurrentOrder.OrderNumber} - Orange Circle Construction (Pty) Ltd";
                var body = $"Dear {supplierName},\n\nPlease find attached Picking Order {CurrentOrder.OrderNumber}.\n\nKind regards,\nOrange Circle Construction (Pty) Ltd";
                bool usedOutlook = EmailHelper.OpenEmailWithAttachment(recipientEmail, subject, body, path);

                if (usedOutlook)
                    _toastService.ShowSuccess("Email Created", $"Outlook opened with Picking Order {CurrentOrder.OrderNumber} attached for {recipientEmail}.");
                else
                    _toastService.ShowInfo("Email Prepared", $"Default mail client opened for {recipientEmail}. PDF location opened in File Explorer.");
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

        // ─── Delete ───────────────────────────────────────────────────────────────

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
