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
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    /// <summary>
    /// ViewModel for the Purchase Order detail / edit screen.
    /// Manages the full lifecycle of a Purchase Order: creation, editing, navigation
    /// between existing orders, saving, PDF preview, email dispatch, and GRV receiving.
    /// </summary>
    public partial class PurchaseOrderDetailViewModel : OverlayHostViewModel
    {
        // ─── Services ─────────────────────────────────────────────────────────────

        private readonly IOrderService _orderService;
        private readonly ISupplierService _supplierService;
        private readonly IProjectService _projectService;
        private readonly IInventoryService _inventoryService;
        private readonly InventoryCacheService _inventoryCache;
        private readonly INavigationService _navigationService;
        private readonly IPdfService _pdfService;
        private readonly IToastService _toastService;
        private readonly IGoogleMapsService _googleMapsService;
        private readonly ISettingsService _settingsService;
        private readonly OCC.WpfClient.Services.Infrastructure.LocalSettingsService _localSettingsService;
        private readonly IAuthService _authService;
        private readonly OCC.WpfClient.Services.Infrastructure.ConnectionSettings _connectionSettings;
        private readonly IDialogService _dialogService;
        private readonly ILogger<PurchaseOrderDetailViewModel> _logger;

        // ─── State Guards ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sentinel used during programmatic population of order/supplier/project fields
        /// to prevent OnSelected* handlers from overwriting just-set values.
        /// Must be set to true before any batch of assignments and false only AFTER all
        /// async resolution is complete (not just the synchronous UI assignments).
        /// </summary>
        private bool _isPopulating;

        /// <summary>
        /// Prevents re-entrant calls to <see cref="LoadDataAsync"/>. A SemaphoreSlim
        /// allows async callers to await rather than silently skip (unlike the old IsBusy
        /// guard which caused the race if navigation fired before IsBusy was cleared).
        /// </summary>
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

        /// <summary>Guards against the "Item not found" dialog being shown multiple times.</summary>
        private bool _isShowingItemNotFoundDialog;

        private static readonly Project OtherProjectSentinel = new() { Id = Guid.Empty, Name = "Other..." };

        // ─── Observable Properties ────────────────────────────────────────────────

        [ObservableProperty]
        private bool _isOtherProjectSelected;

        [ObservableProperty]
        private bool _isCustomProjectSuggestionsOpen;

        public ObservableCollection<string> CustomProjectSuggestions { get; } = new();

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
        private CancellationTokenSource? _addressCts;
        private bool _isHandlingAddressSelection;

        // ─── Constructor ──────────────────────────────────────────────────────────

        public PurchaseOrderDetailViewModel(
            IOrderService orderService,
            ISupplierService supplierService,
            IProjectService projectService,
            IInventoryService inventoryService,
            InventoryCacheService inventoryCache,
            INavigationService navigationService,
            IPdfService pdfService,
            IToastService toastService,
            IDialogService dialogService,
            IGoogleMapsService googleMapsService,
            ISettingsService settingsService,
            LocalSettingsService localSettingsService,
            IAuthService authService,
            ConnectionSettings connectionSettings,
            ILogger<PurchaseOrderDetailViewModel> logger)
        {
            _orderService = orderService;
            _supplierService = supplierService;
            _projectService = projectService;
            _inventoryService = inventoryService;
            _inventoryCache = inventoryCache;
            _navigationService = navigationService;
            _pdfService = pdfService;
            _toastService = toastService;
            _dialogService = dialogService;
            _googleMapsService = googleMapsService;
            _settingsService = settingsService;
            _localSettingsService = localSettingsService;
            _authService = authService;
            _connectionSettings = connectionSettings;
            _logger = logger;

            Title = "Create Purchase Order";
        }

        // ─── CurrentOrder Change Hook ─────────────────────────────────────────────

        partial void OnCurrentOrderChanged(OrderWrapper? oldValue, OrderWrapper? newValue)
        {
            if (oldValue != null)
                oldValue.PropertyChanged -= CurrentOrder_PropertyChanged;

            if (newValue != null)
                newValue.PropertyChanged += CurrentOrder_PropertyChanged;
        }

        /// <summary>
        /// Handles property changes on the current order wrapper.
        /// Triggers address auto-complete when DeliveryInstructions changes in Other mode,
        /// and reloads project name history when the user types in the custom project field.
        /// </summary>
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
            else if (e.PropertyName == nameof(OrderWrapper.ProjectName))
            {
                if (IsOtherProjectSelected && CurrentOrder != null)
                {
                    LoadCustomProjectHistory();
                }
            }
        }

        // ─── Custom Project History ───────────────────────────────────────────────

        /// <summary>Loads the user's custom (non-system) project name history into suggestions.</summary>
        public void LoadCustomProjectHistory()
        {
            var history = _localSettingsService.Settings.CustomProjectHistory ?? new System.Collections.Generic.List<string>();
            CustomProjectSuggestions.Clear();
            foreach (var item in history)
                CustomProjectSuggestions.Add(item);

            // Prepend the current order's project name if it's not already in the history
            if (CurrentOrder != null
                && !string.IsNullOrWhiteSpace(CurrentOrder.ProjectName)
                && CurrentOrder.ProjectName != "Other..."
                && !CustomProjectSuggestions.Contains(CurrentOrder.ProjectName))
            {
                CustomProjectSuggestions.Insert(0, CurrentOrder.ProjectName);
            }
        }

        [RelayCommand]
        public void RemoveCustomProjectSuggestion(string project)
        {
            if (string.IsNullOrWhiteSpace(project)) return;
            _localSettingsService.RemoveCustomProjectHistory(project);
            LoadCustomProjectHistory();
        }

        /// <summary>
        /// Persists the current custom project name to the user's local history when
        /// leaving the Other project text field.
        /// </summary>
        public void AddCurrentCustomProjectToHistory()
        {
            if (IsOtherProjectSelected && CurrentOrder != null && !string.IsNullOrWhiteSpace(CurrentOrder.ProjectName))
            {
                _localSettingsService.AddCustomProjectHistory(CurrentOrder.ProjectName);
                LoadCustomProjectHistory();
            }
        }

        // ─── Main Load Command ────────────────────────────────────────────────────

        /// <summary>
        /// Entry point for loading the screen. Orchestrates lookup data fetching,
        /// order cycling list initialisation, and order population in a defined sequence
        /// to eliminate race conditions between async fetch tasks and UI binding.
        /// </summary>
        [RelayCommand]
        private async Task LoadDataAsync()
        {
            // Use semaphore instead of IsBusy flag — prevents re-entrant loads while still
            // allowing the first load to fully complete before a second can start.
            if (!await _loadSemaphore.WaitAsync(0))
            {
                _logger.LogDebug("LoadDataAsync skipped — already running.");
                return;
            }

            try
            {
                SetBusy(true);
                LoadCustomProjectHistory();

                // ── Phase 1: Fetch all lookup data in parallel on a background thread. ──
                // All three fetches run concurrently. InventoryItems is ALWAYS refreshed so
                // that newly created SKUs are available for line resolution.
                var (suppliers, allProjects, inventory) = await LoadLookupsAsync();

                // ── Phase 2: Assign lookup collections to UI — sync only, no await inside. ──
                SetLookupCollections(suppliers, allProjects, inventory);

                // ── Phase 3: Fetch the order cycling list. ──
                if (!_allOrderIds.Any())
                {
                    var allOrders = await _orderService.GetOrdersAsync();
                    _allOrderIds = allOrders.OrderByDescending(o => o.OrderDate).Select(o => o.Id).ToList();
                }

                // ── Phase 4: Load or create the order, THEN resolve selections. ──
                // _isPopulating is set before any UI assignment and cleared only AFTER
                // all async resolution (supplier + project) has fully completed.
                _isPopulating = true;
                try
                {
                    if (OrderId.HasValue && OrderId.Value != Guid.Empty)
                    {
                        await PopulateExistingOrderAsync(OrderId.Value, allProjects);
                    }
                    else
                    {
                        await PopulateNewOrderAsync();
                    }
                }
                finally
                {
                    // Only release _isPopulating after all async resolution is done.
                    // This guard prevents OnSelectedSupplierChanged / OnSelectedProjectChanged
                    // from overwriting resolved fields mid-population.
                    _isPopulating = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading purchase order details data");
                ErrorMessage = "Failed to load required data. Please try again.";
            }
            finally
            {
                SetBusy(false);
                _loadSemaphore.Release();
            }
        }

        /// <summary>
        /// Fetches suppliers, projects, and inventory in parallel.
        /// Falls back to supplier summaries if the full fetch fails.
        /// </summary>
        private async Task<(IEnumerable<Supplier> suppliers, IEnumerable<Project> allProjects, IEnumerable<InventoryItem> inventory)> LoadLookupsAsync()
        {
            // Run all three fetches concurrently for performance.
            var suppliersTask = LoadSuppliersAsync();
            var projectsTask = LoadProjectsAsync();
            var inventoryTask = LoadInventoryAsync();

            await Task.WhenAll(suppliersTask, projectsTask, inventoryTask);

            return (suppliersTask.Result, projectsTask.Result, inventoryTask.Result);
        }

        private async Task<IEnumerable<Supplier>> LoadSuppliersAsync()
        {
            try
            {
                return await _supplierService.GetSuppliersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load full suppliers, falling back to summaries");
                try
                {
                    var summaries = await _supplierService.GetSupplierSummariesAsync();
                    return summaries.Select(s => new Supplier
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
                    return Enumerable.Empty<Supplier>();
                }
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
                _logger.LogError(ex, "Failed to load projects");
                return Enumerable.Empty<Project>();
            }
        }

        private async Task<IEnumerable<InventoryItem>> LoadInventoryAsync()
        {
            try
            {
                // Use the 5-minute TTL cache to avoid re-hitting the API on every
                // navigation event. The cache is invalidated automatically after
                // the TTL, ensuring newly added SKUs appear within 5 minutes.
                return await _inventoryCache.GetAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load inventory");
                return Enumerable.Empty<InventoryItem>();
            }
        }

        /// <summary>
        /// Assigns fetched lookup data to the observable collections bound in XAML.
        /// This method is purely synchronous — no awaits — so it can be called safely
        /// without the async-lambda-in-Dispatcher pitfall.
        /// </summary>
        private void SetLookupCollections(
            IEnumerable<Supplier> suppliers,
            IEnumerable<Project> allProjects,
            IEnumerable<InventoryItem> inventory)
        {
            // Always refresh suppliers
            Suppliers.Clear();
            foreach (var s in suppliers) Suppliers.Add(s);

            // Always refresh projects
            Projects.Clear();
            var activeProjects = allProjects.Where(p =>
                p.Status != "Completed" && p.Status != "Archived" && p.Status != "Cancelled");
            foreach (var p in activeProjects) Projects.Add(p);
            Projects.Add(OtherProjectSentinel);

            // Always refresh inventory — this is critical for SKU resolution.
            // Previously was short-circuited with !InventoryItems.Any() which meant
            // a cached (potentially stale) list was used, causing SKU mismatches.
            InventoryItems.Clear();
            foreach (var i in inventory) InventoryItems.Add(i);

            _logger.LogDebug(
                "Lookup collections refreshed: {SupplierCount} suppliers, {ProjectCount} projects, {InventoryCount} inventory items",
                Suppliers.Count, Projects.Count - 1, InventoryItems.Count);
        }

        /// <summary>
        /// Fetches an existing order and fully populates the UI, including resolving
        /// supplier and project selections. The caller must have set <c>_isPopulating = true</c>
        /// before calling and must clear it in a <c>finally</c> block.
        /// </summary>
        private async Task PopulateExistingOrderAsync(Guid orderId, IEnumerable<Project> allProjects)
        {
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("GetOrderAsync returned null for order {OrderId}", orderId);
                return;
            }

            // Assign UI state synchronously on the calling thread (already on UI thread via
            // the command executor). No Dispatcher.InvokeAsync needed here — RelayCommand
            // execution already marshals back to the UI thread for WPF.
            CurrentOrder = new OrderWrapper(order);
            _currentIndex = _allOrderIds.IndexOf(order.Id);
            IsNewOrder = false;

            // Resolve supplier and project AFTER CurrentOrder is assigned.
            // These are awaitable but purely update SelectedSupplier / SelectedProject
            // properties which are guarded by _isPopulating — safe to await here.
            ResolveSupplierSelection(order.SupplierId, order.SupplierName);
            await ResolveProjectSelectionAsync(order.ProjectId, order.ProjectName, allProjects);

            _logger.LogInformation(
                "Populated existing order {OrderNumber} with {LineCount} lines",
                order.OrderNumber, order.Lines.Count);
        }

        /// <summary>
        /// Creates a new order template and populates the UI with empty lines.
        /// The caller must have set <c>_isPopulating = true</c> before calling.
        /// </summary>
        private async Task PopulateNewOrderAsync()
        {
            var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
            if (_authService.CurrentUser?.Branch != null)
            {
                order.Branch = _authService.CurrentUser.Branch.Value;
            }

            CurrentOrder = new OrderWrapper(order);
            _currentIndex = -1; // -1 represents "New Order"
            IsNewOrder = true;
            SelectedProject = null;
            SelectedSupplier = null;
            IsOtherProjectSelected = false;

            // Add 10 blank placeholder rows
            for (int i = 0; i < 10; i++)
            {
                AddLine();
            }
        }

        // ─── Selection Resolvers ──────────────────────────────────────────────────

        /// <summary>
        /// Resolves the project combobox selection for a loaded order.
        /// Handles system projects, the "Other..." sentinel, and archived/completed
        /// projects that are no longer in the active projects list.
        /// </summary>
        private async Task ResolveProjectSelectionAsync(Guid? projectId, string? projectName, IEnumerable<Project>? allProjectsList = null)
        {
            _logger.LogInformation(
                "ResolveProjectSelectionAsync: projectId={ProjectId}, projectName='{ProjectName}'",
                projectId, projectName);

            var otherSentinel = Projects.FirstOrDefault(p => p.Id == Guid.Empty) ?? OtherProjectSentinel;
            if (!Projects.Contains(otherSentinel))
                Projects.Add(otherSentinel);

            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                // Look up the active projects list first
                var matchingProject = Projects.FirstOrDefault(p => p.Id == projectId.Value);
                if (matchingProject == null)
                {
                    // Order is linked to a project that has been completed/archived — fetch it
                    var all = allProjectsList ?? await _projectService.GetProjectsAsync();
                    var originalProject = all.FirstOrDefault(p => p.Id == projectId.Value);
                    if (originalProject != null)
                    {
                        // Insert before "Other..." so it appears in the dropdown
                        int otherIndex = Projects.IndexOf(otherSentinel);
                        if (otherIndex >= 0) Projects.Insert(otherIndex, originalProject);
                        else Projects.Add(originalProject);
                        matchingProject = originalProject;
                    }
                }

                SelectedProject = matchingProject;
                _logger.LogInformation(
                    "ResolveProjectSelectionAsync: resolved to '{Name}' ({Id})",
                    SelectedProject?.Name, SelectedProject?.Id);
            }
            else if ((projectId.HasValue && projectId.Value == Guid.Empty) || !string.IsNullOrWhiteSpace(projectName))
            {
                // Custom / "Other..." project
                SelectedProject = otherSentinel;
                _logger.LogInformation(
                    "ResolveProjectSelectionAsync: Other sentinel selected. ProjectName='{Name}'", projectName);

                if (CurrentOrder != null && !string.IsNullOrWhiteSpace(projectName))
                {
                    CurrentOrder.ProjectName = projectName;
                }
                LoadCustomProjectHistory();
            }
            else
            {
                SelectedProject = null;
                _logger.LogInformation("ResolveProjectSelectionAsync: SelectedProject set to null");
            }
        }

        /// <summary>
        /// Resolves the supplier combobox selection for a loaded order.
        /// Falls back to name-matching if the ID is missing, and creates a
        /// placeholder supplier entry if no match is found.
        /// </summary>
        private void ResolveSupplierSelection(Guid? supplierId, string? supplierName)
        {
            Supplier? matchingSupplier = null;

            if (supplierId.HasValue && supplierId.Value != Guid.Empty)
                matchingSupplier = Suppliers.FirstOrDefault(s => s.Id == supplierId.Value);

            if (matchingSupplier == null && !string.IsNullOrWhiteSpace(supplierName))
                matchingSupplier = Suppliers.FirstOrDefault(s => string.Equals(s.Name, supplierName, StringComparison.OrdinalIgnoreCase));

            if (matchingSupplier == null && !string.IsNullOrWhiteSpace(supplierName))
                matchingSupplier = Suppliers.FirstOrDefault(s => s.Name.Contains(supplierName, StringComparison.OrdinalIgnoreCase) || supplierName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));

            if (matchingSupplier == null && (!string.IsNullOrWhiteSpace(supplierName) || (supplierId.HasValue && supplierId.Value != Guid.Empty)))
            {
                // Create a placeholder so the name is displayed even if the supplier was deleted
                matchingSupplier = new Supplier
                {
                    Id = (supplierId.HasValue && supplierId.Value != Guid.Empty) ? supplierId.Value : Guid.NewGuid(),
                    Name = !string.IsNullOrWhiteSpace(supplierName) ? supplierName : "Unknown Supplier"
                };
                Suppliers.Add(matchingSupplier);
            }

            SelectedSupplier = matchingSupplier;
        }

        // ─── Selection Changed Handlers ───────────────────────────────────────────

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            // Guard: do not overwrite order fields while we are programmatically loading
            if (_isPopulating) return;

            if (value != null && CurrentOrder != null)
            {
                CurrentOrder.SupplierId = value.Id;
                CurrentOrder.SupplierName = value.Name;
                CurrentOrder.EntityAddress = value.Address;
                CurrentOrder.EntityTel = value.Phone;
                CurrentOrder.EntityVatNo = value.VatNumber;
            }

            // Lazily fetch full supplier details (including contacts) if needed
            if (value != null && value.Id != Guid.Empty && (value.Contacts == null || !value.Contacts.Any()))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var freshSupplier = await _supplierService.GetSupplierAsync(value.Id);
                        if (freshSupplier?.Contacts != null && freshSupplier.Contacts.Any())
                        {
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                if (SelectedSupplier?.Id == freshSupplier.Id)
                                {
                                    SelectedSupplier = freshSupplier;
                                    var existing = Suppliers.FirstOrDefault(s => s != null && s.Id == freshSupplier.Id);
                                    if (existing != null)
                                    {
                                        var idx = Suppliers.IndexOf(existing);
                                        if (idx >= 0) Suppliers[idx] = freshSupplier;
                                    }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to lazily load supplier contacts for {SupplierId}", value.Id);
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
                    var preservedName = CurrentOrder.ProjectName;
                    CurrentOrder.ProjectId = null;

                    if (!string.IsNullOrWhiteSpace(preservedName) && preservedName != "Other...")
                        CurrentOrder.ProjectName = preservedName;
                    else if (CurrentOrder.ProjectName == null || CurrentOrder.ProjectName == "Other...")
                        CurrentOrder.ProjectName = string.Empty;

                    LoadCustomProjectHistory();
                    IsOtherProjectSelected = true;
                }
                else
                {
                    CurrentOrder.ProjectId = value.Id;
                    CurrentOrder.ProjectName = value.Name;
                    CurrentOrder.Attention = value.ProjectManager ?? string.Empty;
                    IsOtherProjectSelected = false;
                    CustomProjectSuggestions.Clear();
                    IsCustomProjectSuggestionsOpen = false;

                    if (!_isPopulating)
                    {
                        // Auto-select "Site" destination type when user interactively picks a project
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

        /// <summary>
        /// Real-time SKU lookup triggered by the ComboBox SelectionChanged event.
        /// Only updates the line if a matching inventory item is found — does not show
        /// any dialog while the user is still typing.
        /// </summary>
        [RelayCommand]
        private void UpdateLineItem(OrderLineWrapper line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.ItemCode)) return;

            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                bool isNewItem = line.InventoryItemId != item.Id;
                line.InventoryItemId = item.Id;

                if (isNewItem || string.IsNullOrWhiteSpace(line.Description))
                    line.Description = item.Description;
                if (isNewItem || string.IsNullOrWhiteSpace(line.UnitOfMeasure))
                    line.UnitOfMeasure = item.UnitOfMeasure;
                if (isNewItem || line.UnitPrice == 0)
                    line.UnitPrice = item.AverageCost;

                line.UpdateCalculations();
            }
        }

        /// <summary>
        /// Final validation triggered when the SKU field loses focus.
        /// If the typed SKU is not in inventory, prompts the user to create a new item.
        /// The <see cref="OrderLineWrapper.LastValidatedSku"/> flag prevents the dialog
        /// from repeating if the user tabs away without changing the code.
        /// </summary>
        [RelayCommand]
        private void ValidateLineItem(OrderLineWrapper line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.ItemCode)) return;

            // Suppress repeat validation for the same SKU value
            if (line.ItemCode.Equals(line.LastValidatedSku, StringComparison.OrdinalIgnoreCase)) return;

            var item = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                line.LastValidatedSku = line.ItemCode;
                bool isNewItem = line.InventoryItemId != item.Id;
                line.InventoryItemId = item.Id;

                if (isNewItem || string.IsNullOrWhiteSpace(line.Description))
                    line.Description = item.Description;
                if (isNewItem || string.IsNullOrWhiteSpace(line.UnitOfMeasure))
                    line.UnitOfMeasure = item.UnitOfMeasure;
                if (isNewItem || line.UnitPrice == 0)
                    line.UnitPrice = item.AverageCost;

                line.UpdateCalculations();
            }
            else
            {
                if (_isShowingItemNotFoundDialog) return;
                _isShowingItemNotFoundDialog = true;
                line.LastValidatedSku = line.ItemCode;

                var dialog = new ItemNotFoundViewModel(line.ItemCode);
                dialog.Completed += (wantsToCreate) =>
                {
                    _isShowingItemNotFoundDialog = false;
                    CloseOverlay();
                    if (wantsToCreate)
                        ShowNewItemDialog(line);
                };
                OpenOverlay(dialog);
            }
        }

        // ─── Validation & Save ────────────────────────────────────────────────────

        /// <summary>
        /// Validates the order and prepares it for persistence.
        /// Resolves any missing supplier/inventory references, strips blank lines,
        /// and guards against past ETA dates.
        /// </summary>
        private bool ValidateAndPrepareOrderForSave()
        {
            if (CurrentOrder == null) return false;

            // 1. Resolve supplier if it became unlinked (can happen if WPF clears the binding transiently)
            if (SelectedSupplier == null)
                ResolveSupplierSelection(CurrentOrder.SupplierId, CurrentOrder.SupplierName);

            if (SelectedSupplier != null)
            {
                CurrentOrder.SupplierId = SelectedSupplier.Id;
                CurrentOrder.SupplierName = SelectedSupplier.Name;
                if (string.IsNullOrWhiteSpace(CurrentOrder.EntityAddress)) CurrentOrder.EntityAddress = SelectedSupplier.Address;
                if (string.IsNullOrWhiteSpace(CurrentOrder.EntityTel)) CurrentOrder.EntityTel = SelectedSupplier.Phone;
                if (string.IsNullOrWhiteSpace(CurrentOrder.EntityVatNo)) CurrentOrder.EntityVatNo = SelectedSupplier.VatNumber;
            }

            if (SelectedSupplier == null
                && (CurrentOrder.SupplierId == null || CurrentOrder.SupplierId == Guid.Empty)
                && string.IsNullOrWhiteSpace(CurrentOrder.SupplierName))
            {
                _toastService.ShowError("Save Failed", "Please select a supplier for the purchase order.");
                return false;
            }

            // 2. Persist custom project name to local history
            if (IsOtherProjectSelected && CurrentOrder != null)
            {
                CurrentOrder.ProjectId = null;
                if (!string.IsNullOrWhiteSpace(CurrentOrder.ProjectName))
                    AddCurrentCustomProjectToHistory();
            }

            _logger.LogInformation(
                "ValidateAndPrepareOrderForSave: IsOtherProjectSelected={IsOther}, ProjectId={ProjectId}, ProjectName='{ProjectName}'",
                IsOtherProjectSelected, CurrentOrder?.ProjectId, CurrentOrder?.ProjectName);

            // 3. Resolve missing InventoryItemIds for typed SKUs
            foreach (var line in CurrentOrder!.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.ItemCode)
                    && (!line.InventoryItemId.HasValue || line.InventoryItemId.Value == Guid.Empty))
                {
                    var match = InventoryItems.FirstOrDefault(i => i.Sku.Equals(line.ItemCode, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        line.InventoryItemId = match.Id;
                }
            }

            // 4. Strip completely blank placeholder lines (no code AND no description)
            var blankLines = CurrentOrder.Lines
                .Where(l => string.IsNullOrWhiteSpace(l.ItemCode) && string.IsNullOrWhiteSpace(l.Description))
                .ToList();
            foreach (var line in blankLines)
                CurrentOrder.Lines.Remove(line);

            // 5. Require at least one valid line
            if (!CurrentOrder.Lines.Any())
            {
                _toastService.ShowError("Save Failed", "Please add at least one line item to the purchase order.");
                return false;
            }

            // 6. Ensure ETA is not in the past
            if (CurrentOrder.ExpectedDeliveryDate.HasValue && CurrentOrder.ExpectedDeliveryDate.Value.Date < DateTime.Today)
                CurrentOrder.ExpectedDeliveryDate = DateTime.Today.AddDays(7);

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

                    // Wrap the saved result (server may have updated fields e.g. Id, OrderDate)
                    _isPopulating = true;
                    try
                    {
                        CurrentOrder = new OrderWrapper(savedOrder);
                        OrderId = savedOrder.Id;
                        IsNewOrder = false;

                        if (_currentIndex == -1)
                        {
                            _allOrderIds.Insert(0, savedOrder.Id);
                            _currentIndex = 0;
                        }

                        ResolveSupplierSelection(CurrentOrder.SupplierId, CurrentOrder.SupplierName);
                        await ResolveProjectSelectionAsync(CurrentOrder.ProjectId, CurrentOrder.ProjectName);
                    }
                    finally
                    {
                        _isPopulating = false;
                    }
                }
                else
                {
                    await _orderService.UpdateOrderAsync(CurrentOrder.Model);
                }

                if (showToast)
                    _toastService.ShowSuccess("Order Saved", $"Purchase Order {CurrentOrder.OrderNumber} saved successfully.");

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

        // ─── Save Commands ────────────────────────────────────────────────────────

        /// <summary>Saves the purchase order and closes the detail view.</summary>
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

        /// <summary>Saves the purchase order and resets to a blank new order template.</summary>
        [RelayCommand]
        private async Task SaveAndNewAsync()
        {
            bool saved = await SaveOrderWithoutClosingAsync(showToast: true);
            if (!saved) return;

            try
            {
                IsBusy = true;
                var order = await _orderService.CreateNewOrderTemplateAsync(OrderType.PurchaseOrder);
                if (_authService.CurrentUser?.Branch != null)
                    order.Branch = _authService.CurrentUser.Branch.Value;

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
                    order.Branch = _authService.CurrentUser.Branch.Value;

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
        private Task CancelAsync()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            return Task.CompletedTask;
        }

        // ─── Navigation ───────────────────────────────────────────────────────────

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

        /// <summary>External entry point used by the Procurement list view to open a specific order.</summary>
        public async Task LoadOrderAsync(Guid id)
        {
            OrderId = id;
            await LoadDataAsync();
        }

        /// <summary>
        /// Loads an order by ID for prev/next navigation cycling.
        /// Uses a separate semaphore wait to prevent concurrent navigation.
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
                        ResolveSupplierSelection(order.SupplierId, order.SupplierName);
                        // Await project resolution fully before releasing _isPopulating
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

        // ─── Find Order Dialog ─────────────────────────────────────────────────────

        [RelayCommand]
        private void FindOrder()
        {
            var dialog = new FindOrderViewModel(_orderService, _supplierService);
            dialog.CloseRequested += CloseOverlay;

            // Use an explicit async handler to avoid the fire-and-forget pattern.
            // Previously this was an async lambda that was not awaited, causing
            // _isPopulating to release before resolution completed.
            dialog.OrderSelected += OnOrderSelectedAsync;

            OpenOverlay(dialog);
        }

        /// <summary>
        /// Handles the order selection event from the FindOrder dialog.
        /// Wraps the async work in a named method so it can be properly connected
        /// (and disconnected) as an event handler without fire-and-forget.
        /// </summary>
        private async void OnOrderSelectedAsync(Order order)
        {
            _isPopulating = true;
            try
            {
                CurrentOrder = new OrderWrapper(order);
                ResolveSupplierSelection(order.SupplierId, order.SupplierName);
                // Await fully — previously this was inside an async lambda that was
                // fire-and-forgot, causing _isPopulating to be cleared while still running
                await ResolveProjectSelectionAsync(order.ProjectId, order.ProjectName);
                _currentIndex = _allOrderIds.IndexOf(order.Id);
                IsNewOrder = false;
            }
            finally
            {
                _isPopulating = false;
            }

            CloseOverlay();
        }

        // ─── PDF / Print / Email Commands ─────────────────────────────────────────

        /// <summary>Saves the purchase order then generates and opens the PDF preview.</summary>
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

        /// <summary>Saves the purchase order then triggers PDF for printing (opens in viewer).</summary>
        [RelayCommand]
        private async Task PrintOrderAsync()
        {
            await PreviewOrderAsync();
        }

        /// <summary>Saves the purchase order then generates the PDF and opens the email client.</summary>
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

                // Always fetch fresh supplier to ensure contacts are loaded
                var supplierId = CurrentOrder?.SupplierId ?? SelectedSupplier?.Id ?? Guid.Empty;
                Supplier? emailSupplier = null;

                if (supplierId != Guid.Empty)
                {
                    try
                    {
                        emailSupplier = await _supplierService.GetSupplierAsync(supplierId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not fetch full supplier for {SupplierId} when preparing email", supplierId);
                    }
                }

                if (emailSupplier == null) emailSupplier = SelectedSupplier;

                if (emailSupplier != null)
                {
                    SelectedSupplier = emailSupplier;
                    var existing = Suppliers.FirstOrDefault(s => s != null && s.Id == emailSupplier.Id);
                    if (existing != null)
                    {
                        var idx = Suppliers.IndexOf(existing);
                        if (idx >= 0) Suppliers[idx] = emailSupplier;
                    }
                }

                // Collect all valid email addresses
                var emails = new List<string>();
                if (emailSupplier != null)
                {
                    if (!string.IsNullOrWhiteSpace(emailSupplier.Email))
                    {
                        foreach (var e in EmailHelper.ParseEmailAddresses(emailSupplier.Email))
                            if (!emails.Contains(e, StringComparer.OrdinalIgnoreCase)) emails.Add(e);
                    }

                    if (emailSupplier.Contacts != null)
                    {
                        foreach (var contact in emailSupplier.Contacts)
                        {
                            if (!string.IsNullOrWhiteSpace(contact.Email))
                            {
                                foreach (var ce in EmailHelper.ParseEmailAddresses(contact.Email))
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
                                    SelectedSupplier.Email = newContact.Email;
                                if (string.IsNullOrWhiteSpace(SelectedSupplier.ContactPerson))
                                    SelectedSupplier.ContactPerson = newContact.ContactName;

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
                    var tcs = new TaskCompletionSource<SupplierContact?>();
                    var dialog = new AddSupplierContactViewModel(SelectedSupplier?.Name ?? "Supplier");
                    dialog.Completed += (newContact) =>
                    {
                        CloseOverlay();
                        tcs.TrySetResult(newContact);
                    };
                    OpenOverlay(dialog);
                    var newContact = await tcs.Task;
                    if (newContact == null || string.IsNullOrWhiteSpace(newContact.Email)) return;

                    recipientEmail = newContact.Email.Trim();

                    if (SelectedSupplier != null)
                    {
                        newContact.SupplierId = SelectedSupplier.Id;
                        SelectedSupplier.Contacts ??= new List<SupplierContact>();
                        SelectedSupplier.Contacts.Add(newContact);

                        if (string.IsNullOrWhiteSpace(SelectedSupplier.Email))
                            SelectedSupplier.Email = newContact.Email;
                        if (string.IsNullOrWhiteSpace(SelectedSupplier.ContactPerson))
                            SelectedSupplier.ContactPerson = newContact.ContactName;

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

                var orderNum = CurrentOrder?.OrderNumber ?? "PO";
                var subject = $"Purchase Order {orderNum} - Orange Circle Construction (Pty) Ltd";
                var body = $"Dear {contactPerson},\n\nPlease find attached Purchase Order {orderNum}.\n\nKind regards,\nOrange Circle Construction (Pty) Ltd";

                bool usedOutlook = EmailHelper.OpenEmailWithAttachment(recipientEmail, subject, body, path);

                if (usedOutlook)
                    _toastService.ShowSuccess("Email Created", $"Outlook opened with Purchase Order {orderNum} attached for {recipientEmail}.");
                else
                    _toastService.ShowInfo("Email Prepared", $"Default mail client opened for {recipientEmail}. PDF location opened in File Explorer.");
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

        // ─── Delete ───────────────────────────────────────────────────────────────

        [RelayCommand]
        private Task DeleteOrderAsync()
        {
            if (CurrentOrder == null) return Task.CompletedTask;
            WeakReferenceMessenger.Default.Send(new OpenHubMessage(NavigationRoutes.Procurement));
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
            return Task.CompletedTask;
        }

        // ─── New Item Dialog ──────────────────────────────────────────────────────

        private void ShowNewItemDialog(OrderLineWrapper line)
        {
            var dialog = new NewItemViewModel(line.ItemCode, _inventoryService);
            dialog.Completed += (newItem) =>
            {
                CloseOverlay();
                if (newItem != null)
                {
                    // Update the local observable and the shared cache so other ViewModels
                    // (e.g. PickingOrderViewModel) see the new SKU without waiting for TTL.
                    InventoryItems.Add(newItem);
                    _inventoryCache.AddItem(newItem);
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

        // ─── Line Cleanup Helper ──────────────────────────────────────────────────

        private void CleanupEmptyLines()
        {
            if (CurrentOrder == null) return;
            var emptyLines = CurrentOrder.Lines
                .Where(l => string.IsNullOrWhiteSpace(l.ItemCode)
                         && string.IsNullOrWhiteSpace(l.Description)
                         && l.QuantityOrdered == 0
                         && l.UnitPrice == 0)
                .ToList();
            foreach (var line in emptyLines)
                CurrentOrder.Lines.Remove(line);
        }

        // ─── Address Autocomplete ─────────────────────────────────────────────────

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

            if (string.IsNullOrEmpty(_connectionSettings.GoogleApiKey))
            {
                var key = await _settingsService.GetGoogleMapsKeyAsync();
                if (!string.IsNullOrEmpty(key))
                    _connectionSettings.GoogleApiKey = key;
            }

            if (string.IsNullOrWhiteSpace(_connectionSettings.GoogleApiKey)) return;

            _addressCts?.Cancel();
            _addressCts = new CancellationTokenSource();
            var token = _addressCts.Token;

            try
            {
                await Task.Delay(300, token);
                var suggestions = await _googleMapsService.GetAddressSuggestionsAsync(CurrentOrder.DeliveryAddress, _addressSessionToken);
                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    AddressSuggestions.Clear();
                    foreach (var s in suggestions ?? Array.Empty<AddressSuggestion>())
                        AddressSuggestions.Add(s);
                });
            }
            catch (OperationCanceledException) { /* debounce cancellation — expected */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Address Search Error");
            }
        }

        partial void OnSelectedAddressSuggestionChanged(AddressSuggestion? value)
        {
            if (value != null)
                _ = HandleAddressSelectionAsync(value);
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

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets <see cref="IsBusy"/> on the correct thread.
        /// Avoids the repetitive dispatcher null-check pattern throughout the class.
        /// </summary>
        private void SetBusy(bool busy)
        {
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
                && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => IsBusy = busy);
            }
            else
            {
                IsBusy = busy;
            }
        }
    }
}
