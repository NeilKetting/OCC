using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Client.Messages;
using OCC.Client.ModelWrappers; // NEW
using OCC.Client.Services;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Features.OrdersHub.UseCases;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for creating, editing, and viewing purchase orders, sales orders, and returns.
    /// Manages complex order states, line item calculations, and coordinates with inventory and supplier services via the Order Manager.
    /// </summary>
    public partial class CreateOrderViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly OrderStateService _orderStateService;
        private readonly ILogger<CreateOrderViewModel> _logger;
        private readonly IPdfService _pdfService;
        private readonly IOrderLifecycleService _lifecycle;
        private readonly OrderSubmissionUseCase _submissionUseCase;
        private readonly IServiceProvider _serviceProvider;
        private readonly IToastService _toastService;

        private bool _isNewOrder;
        private bool _isInitializing;
        private bool _isShowingItemNotFoundDialog;

        #endregion

        #region Observables

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
        private OrderWrapper _currentOrder = null!;

        public string SubmitButtonText => !_isNewOrder ? "UPDATE ORDER" : "CREATE ORDER";

        [ObservableProperty]
        private bool _isReadOnly;

        [ObservableProperty]
        private InventoryLookupViewModel _inventory;

        [ObservableProperty]
        private SupplierSelectorViewModel _suppliers;

        [ObservableProperty]
        private OrderLinesViewModel _lines;

        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<Project> Projects { get; } = new();
        
        public OrderMenuViewModel OrderMenu { get; }

        public List<string> AvailableUOMs { get; } = new() { "ea", "m", "kg", "L", "m2", "m3", "box", "roll", "pack" };
        public List<Branch> Branches { get; } = Enum.GetValues<Branch>().Cast<Branch>().ToList();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private bool _isPurchaseOrder;
        
        [ObservableProperty]
        private bool _isPickingOrder;
        
        [ObservableProperty]
        private bool _isReturnOrder;

        [ObservableProperty]
        private bool _isOfficeDelivery = true;
        
        [ObservableProperty]
        private bool _isSiteDelivery;
        
        [ObservableProperty]
        [property: CustomValidation(typeof(CreateOrderViewModel), nameof(ValidateProjectSelection))]
        private Project? _selectedProject;

        [ObservableProperty]
        private bool _shouldPrintOrder;

        [ObservableProperty]
        private bool _shouldEmailOrder;

        [ObservableProperty]
        private bool _isAddingProjectAddress;
        
        [ObservableProperty]
        private string _newProjectAddress = string.Empty;

        [ObservableProperty]
        private ItemDetailViewModel? _detailViewModel;

        [ObservableProperty]
        private bool _isDetailVisible;

        #endregion

        #region Constructors

        public CreateOrderViewModel(
            IOrderManager orderManager,
            IDialogService dialogService,
            IAuthService authService,
            ILogger<CreateOrderViewModel> logger,
            IPdfService pdfService,
            OrderStateService orderStateService,
            IOrderLifecycleService lifecycle,
            OrderSubmissionUseCase submissionUseCase,
            IServiceProvider serviceProvider,
            IToastService toastService,
            OrderMenuViewModel orderMenu,
            OrderLinesViewModel lines,
            InventoryLookupViewModel inventory,
            SupplierSelectorViewModel suppliers)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _authService = authService;
            _logger = logger;
            _pdfService = pdfService;
            _orderStateService = orderStateService;
            _lifecycle = lifecycle;
            _submissionUseCase = submissionUseCase;
            _serviceProvider = serviceProvider;
            _toastService = toastService;

            OrderMenu = orderMenu;
            Lines = lines;
            Inventory = inventory;
            Suppliers = suppliers;

            OrderMenu.TabSelected += OnMenuTabSelected;
            
            // Forward selection changes
            Inventory.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Inventory.SelectedItem)) OnSelectedInventoryItemChanged(Inventory.SelectedItem);
            };
            Suppliers.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Suppliers.SelectedSupplier)) OnSelectedSupplierChanged(Suppliers.SelectedSupplier);
            };

            // Initialize with a dummy order to satisfy bindings until LoadData runs
            CurrentOrder = new OrderWrapper(new Order { OrderDate = DateTime.Now, Branch = Branch.CPT });
        }

        public CreateOrderViewModel() 
        {
            _orderManager = null!;
            _dialogService = null!;
            _authService = null!;
            _logger = null!;
            _pdfService = null!;
            _orderStateService = null!;
            _lifecycle = null!;
            _submissionUseCase = null!;
            _serviceProvider = null!;
            _toastService = null!;
            
            // Design-time instantiation
            OrderMenu = new OrderMenuViewModel();
            Inventory = new InventoryLookupViewModel();
            Suppliers = new SupplierSelectorViewModel();
            Lines = new OrderLinesViewModel();
            
            CurrentOrder = new OrderWrapper(new Order 
            { 
                OrderNumber = "DESIGN-TIME", 
                OrderDate = DateTime.Now,
                Branch = Branch.CPT 
            });
            CurrentOrder.Lines.Add(new OrderLineWrapper(new OrderLine { 
                Description = "Design Time Item", 
                QuantityOrdered = 1, 
                UnitPrice = 100, 
                LineTotal = 100 
            }));
        }


        #endregion

        #region Commands

        [RelayCommand]
        public void ChangeOrderType(OrderType type)
        {
             CurrentOrder.OrderType = type;
             var temp = _orderManager.CreateNewOrderTemplate(type);
             CurrentOrder.OrderNumber = temp.OrderNumber;
             UpdateOrderTypeFlags();
             OnPropertyChanged(nameof(CurrentOrder));
        }

        [RelayCommand]
        public void ToggleQuickAddProduct()
        {
            DetailViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ItemDetailViewModel>(_serviceProvider);
            
            // Pass current search text as description if available
            var item = new InventoryItem { Description = Inventory.SearchText };
            var categories = Inventory.Categories.ToList();
            
            DetailViewModel.Load(item, categories);

            DetailViewModel.CloseRequested += (s, e) => IsDetailVisible = false;
            DetailViewModel.ItemSaved += async (s, e) =>
            {
                IsDetailVisible = false;
                
                // Refresh inventory cache/list to include new item
                await LoadData(); 
                
                // Try to find and select the new item in the current order line
                if (DetailViewModel.IsEditMode == false) // It was a new item
                {
                    var newItem = Inventory.FilteredItems.FirstOrDefault(i => i.Description == DetailViewModel.Description || i.Sku == DetailViewModel.Sku);
                    if (newItem != null)
                    {
                        Inventory.SelectedItem = newItem;
                    }
                }
            };

            IsDetailVisible = true;
        }

        [RelayCommand] public void ToggleQuickAddSupplier() => Suppliers.ToggleQuickAdd();
        [RelayCommand] public async Task QuickCreateSupplier() => await Suppliers.QuickCreateSupplier();

        [RelayCommand]
        public async Task PreviewOrder()
        {
             try
             {
                 if (Suppliers.SelectedSupplier == null && IsPurchaseOrder)
                 {
                      await _dialogService.ShowAlertAsync("Validation", "Please select a supplier first.");
                      return;
                 }
                 var path = await _pdfService.GenerateOrderPdfAsync(GetSanitizedOrder());
                 new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } }.Start();
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "Failed to preview order");
                 await _dialogService.ShowAlertAsync("Error", "Failed to generate print preview.");
             }
        }

        [RelayCommand]
        public async Task DownloadPrint()
        {
             try
             {
                 if (Suppliers.SelectedSupplier == null && IsPurchaseOrder)
                 {
                      await _dialogService.ShowAlertAsync("Validation", "Please select a supplier first.");
                      return;
                 }
                 var path = await _pdfService.GenerateOrderPdfAsync(GetSanitizedOrder(), isPrintVersion: true);
                 new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } }.Start();
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "Failed to generate print version");
                  await _dialogService.ShowAlertAsync("Error", "Failed to generate grayscale version.");
             }
        }
        
        [RelayCommand]
        public async Task EmailOrder()
        {
             try
             {
                 if (Suppliers.SelectedSupplier == null && IsPurchaseOrder)
                 {
                      await _dialogService.ShowAlertAsync("Validation", "Please select a supplier first.");
                      return;
                 }
                 var path = await _pdfService.GenerateOrderPdfAsync(GetSanitizedOrder(), isPrintVersion: false);
                 new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } }.Start();
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "Failed to generate email version");
                 await _dialogService.ShowAlertAsync("Error", "Failed to generate color version.");
             }
        }

        private Order GetSanitizedOrder()
        {
            CurrentOrder.CommitToModel();
            var model = CurrentOrder.Model;
            var meaningfulLines = model.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l.ItemCode) || !string.IsNullOrWhiteSpace(l.Description))
                .Where(l => l.QuantityOrdered > 0)
                .ToList();
            model.Lines = new ObservableCollection<OrderLine>(meaningfulLines);
            return model;
        }

        [RelayCommand(CanExecute = nameof(CanSubmitOrder))]
        public async Task SubmitOrder()
        {
            if (IsBusy) return;

            var options = new UseCases.OrderSubmissionOptions(ShouldPrintOrder, ShouldEmailOrder, _isNewOrder);
            var (success, result) = await _submissionUseCase.ExecuteAsync(CurrentOrder, options);

            if (success)
            {
                OrderCreated?.Invoke(this, EventArgs.Empty);
                Reset();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            Reset();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public void GoBack()
        {
            Reset();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanFinalise))]
        public async Task FinaliseOrder()
        {
             if (IsBusy) return;
             
             bool confirm = await _dialogService.ShowConfirmationAsync("Finalise Order", "Are you sure you want to finalise this order? It will no longer be editable.");
             if (!confirm) return;

             CurrentOrder.Status = OrderStatus.Finalised;
             
             // Save changes
             var options = new UseCases.OrderSubmissionOptions(ShouldPrintOrder, ShouldEmailOrder, _isNewOrder);
             var (success, result) = await _submissionUseCase.ExecuteAsync(CurrentOrder, options);

             if (success)
             {
                 // Update UI state
                 IsReadOnly = true;
                 Lines.IsReadOnly = true;
                 OnPropertyChanged(nameof(SubmitButtonText));
                 _toastService?.ShowSuccess("Order Finalised", "The order has been finalised successfully.");
             }
             else
             {
                 // Revert status on failure
                 CurrentOrder.Status = OrderStatus.Completed;
             }
        }

        [RelayCommand]
        public void ToggleAddProjectAddress() => IsAddingProjectAddress = !IsAddingProjectAddress;

        [RelayCommand]
        public async Task SaveProjectAddress()
        {
             if (string.IsNullOrWhiteSpace(NewProjectAddress) || SelectedProject == null) return;
             
             try
             {
                 var project = await _orderManager.GetProjectByIdAsync(SelectedProject.Id);
                 if (project != null)
                 {
                     project.StreetLine1 = NewProjectAddress;
                     await _orderManager.UpdateProjectAsync(project);
                     SelectedProject.StreetLine1 = NewProjectAddress;
                     CurrentOrder.EntityAddress = NewProjectAddress;
                     OnPropertyChanged(nameof(CurrentOrder));
                     IsAddingProjectAddress = false;
                 }
             }
             catch(Exception ex)
             {
                 _logger.LogError(ex, "Failed to update project address");
                 await _dialogService.ShowAlertAsync("Error", "Failed to save project address.");
             }
        }

        #endregion

        #region Methods

        public event EventHandler? OrderCreated;
        public event EventHandler? CloseRequested;

        private void Initialize()
        {
            ErrorsChanged -= OnErrorsChanged;
            ErrorsChanged += OnErrorsChanged;
            CurrentOrder.ErrorsChanged -= OnCurrentOrderErrorsChanged;
            CurrentOrder.ErrorsChanged += OnCurrentOrderErrorsChanged;
            
            ValidateAllProperties();
            CurrentOrder.Validate();
            
            SubmitOrderCommand.NotifyCanExecuteChanged();
        }

        private void OnErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e) => SubmitOrderCommand.NotifyCanExecuteChanged();
        private void OnCurrentOrderErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e) => SubmitOrderCommand.NotifyCanExecuteChanged();

        public bool CanSubmitOrder => !HasErrors && !CurrentOrder.HasErrors;

        public bool CanFinalise => CurrentOrder.Status == OrderStatus.Completed;

        public async Task LoadData() => await _lifecycle.LoadInitialDataAsync(this);
        public void Reset() => _lifecycle.Reset(this);
        public async Task LoadExistingOrder(Order order) => await _lifecycle.LoadOrderAsync(this, order);

        public void PrepareForOrder(Order order, bool isNew = false)
        {
            _isInitializing = true;
            try
            {
                _isNewOrder = isNew;
                CurrentOrder = new OrderWrapper(order);
            
            if (CurrentOrder.SupplierId.HasValue)
                Suppliers.SelectedSupplier = Suppliers.FilteredSuppliers.FirstOrDefault(s => s.Id == CurrentOrder.SupplierId.Value);
            else if (!string.IsNullOrEmpty(CurrentOrder.SupplierName))
                Suppliers.SelectedSupplier = Suppliers.FilteredSuppliers.FirstOrDefault(s => s.Name.Equals(CurrentOrder.SupplierName, StringComparison.OrdinalIgnoreCase));

            if (CurrentOrder.CustomerId.HasValue)
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == CurrentOrder.CustomerId.Value);

            if (CurrentOrder.ProjectId.HasValue)
                SelectedProject = Projects.FirstOrDefault(p => p.Id == CurrentOrder.ProjectId.Value);

            IsSiteDelivery = CurrentOrder.DestinationType == OrderDestinationType.Site;
            IsOfficeDelivery = !IsSiteDelivery;
            IsReadOnly = CurrentOrder.Status == OrderStatus.Finalised || CurrentOrder.Status == OrderStatus.Cancelled;

            Lines.IsReadOnly = IsReadOnly;
            Lines.Initialize(CurrentOrder, Enumerable.Empty<InventoryItem>()); // Master list is managed centraly

            UpdateOrderTypeFlags();
            
            // For new orders, default to Office delivery (Stock) and user's branch
            if (isNew)
            {
                // Force reset to ensure UI updates if it was already true but UI didn't catch it
                // We do this by setting current order properties first
                CurrentOrder.DestinationType = OrderDestinationType.Stock;
                
                // Update properties
                IsSiteDelivery = false;
                IsOfficeDelivery = true;

                if (_authService.CurrentUser?.Branch != null)
                {
                    CurrentOrder.Branch = _authService.CurrentUser.Branch.Value;
                }
                
                // Explicitly notify change to ensure RadioButton updates
                OnPropertyChanged(nameof(IsOfficeDelivery));
                OnPropertyChanged(nameof(IsSiteDelivery));
            }

            OnPropertyChanged(nameof(SubmitButtonText));
            Initialize();

            // Refresh CanFinalise state
            FinaliseOrderCommand.NotifyCanExecuteChanged();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void UpdateOrderTypeFlags()
        {
            IsPurchaseOrder = CurrentOrder.OrderType == OrderType.PurchaseOrder;
            IsPickingOrder = CurrentOrder.OrderType == OrderType.PickingOrder;
            IsReturnOrder = CurrentOrder.OrderType == OrderType.ReturnToInventory;
        }

        public static ValidationResult? ValidateProjectSelection(Project? project, ValidationContext context)
        {
            var vm = (CreateOrderViewModel)context.ObjectInstance;
            // Only require a project if it is a Picking Order OR if Site Delivery is explicitly selected
            if (vm.IsPickingOrder && project == null) return new ValidationResult("Project is required for Picking Orders.");
            if (vm.IsSiteDelivery && project == null) return new ValidationResult("Project is required for Site Delivery.");
            
            return ValidationResult.Success;
        }

        partial void OnCurrentOrderChanged(OrderWrapper value) 
        {
            if (value != null)
            {
                value.PropertyChanged -= OnOrderPropertyChanged;
                value.PropertyChanged += OnOrderPropertyChanged;
            }
            UpdateOrderTypeFlags();
            Suppliers.Filter(CurrentOrder?.Branch ?? Branch.CPT);
        }

        private void OnOrderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderWrapper.Branch))
            {
                Suppliers.Filter(CurrentOrder?.Branch ?? Branch.CPT);
            }
            else if (e.PropertyName == nameof(OrderWrapper.Status))
            {
                FinaliseOrderCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(OrderWrapper.DestinationType))
            {
                 var dest = CurrentOrder.DestinationType;
                 
                 // Update visibility flags
                 IsSiteDelivery = dest == OrderDestinationType.Site;
                 IsOfficeDelivery = dest == OrderDestinationType.Stock; // Or !IsSiteDelivery

                 if (dest == OrderDestinationType.Stock)
                 {
                     // Clear selected project as it's not applicable for Office/Stock delivery
                     SelectedProject = null;

                     // Set Branch based on User
                     if (_authService.CurrentUser?.Branch != null)
                     {
                         CurrentOrder.Branch = _authService.CurrentUser.Branch.Value;
                     }
                 }
                 else if (dest == OrderDestinationType.Site)
                 {
                     // Potentially clear or set address logic here if needed
                 }
            }
        }

        partial void OnIsOfficeDeliveryChanged(bool value) 
        { 
            // Logic moved to OnOrderPropertyChanged handling DestinationType
        }

        partial void OnIsSiteDeliveryChanged(bool value) 
        { 
            // Logic moved to OnOrderPropertyChanged handling DestinationType
        }
        
        partial void OnSelectedProjectChanged(Project? value)
        {
             if (value != null)
             {
                 CurrentOrder.ProjectId = value.Id;
                 CurrentOrder.ProjectName = value.Name;
                 if (IsPickingOrder || IsSiteDelivery) CurrentOrder.EntityAddress = value.StreetLine1;
                 OnPropertyChanged(nameof(CurrentOrder));
             }
             else
             {
                 CurrentOrder.ProjectId = null;
                 CurrentOrder.ProjectName = string.Empty;
             }
        }

        [RelayCommand]
        public async Task ValidateItemSearch(string searchText)
        {
             if (string.IsNullOrWhiteSpace(searchText) || _isShowingItemNotFoundDialog) return;
             
             // Check if item exists in master list
             var exists = Lines.AllInventoryItems.Any(i => 
                (i.Sku?.Equals(searchText, StringComparison.OrdinalIgnoreCase) ?? false) || 
                (i.Description?.Equals(searchText, StringComparison.OrdinalIgnoreCase) ?? false));

             if (!exists)
             {
                 _isShowingItemNotFoundDialog = true;
                 try
                 {
                     bool create = await _dialogService.ShowConfirmationAsync("Item Not Found", $"'{searchText}' not found.\n\nCreate it now?");
                     if (create)
                     {
                         _orderStateService.SaveState(CurrentOrder.Model, Lines.NewLine.Model, searchText);
                         WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("InventoryDetail_Create", searchText));
                     }
                 }
                 finally
                 {
                     _isShowingItemNotFoundDialog = false;
                 }
             }
        }

        private void OnSelectedInventoryItemChanged(InventoryItem? value)
        {
            if (value != null)
            {
                Lines.NewLine = new OrderLineWrapper(new OrderLine 
                { 
                    InventoryItemId = value.Id, 
                    Description = value.Description ?? "", 
                    ItemCode = value.Sku ?? "", 
                    UnitOfMeasure = value.UnitOfMeasure ?? "", 
                    UnitPrice = value.AverageCost 
                });
                
                if (Suppliers.SelectedSupplier == null && !string.IsNullOrWhiteSpace(value.Supplier))
                {
                    var match = Suppliers.FilteredSuppliers.FirstOrDefault(s => s.Name.Equals(value.Supplier, StringComparison.OrdinalIgnoreCase));
                    if (match != null) Suppliers.SelectedSupplier = match;
                }
            }
        }

        private void OnSelectedSupplierChanged(Supplier? value)
        {
            if (value != null && !_isInitializing)
            {
                CurrentOrder.SupplierId = value.Id;
                CurrentOrder.SupplierName = value.Name;
                CurrentOrder.EntityAddress = value.Address;
                CurrentOrder.EntityTel = value.Phone;
                CurrentOrder.EntityVatNo = value.VatNumber;
                
                // Only overwrite attention if it's a new order or currently empty
                if (_isNewOrder || string.IsNullOrWhiteSpace(CurrentOrder.Attention))
                {
                    CurrentOrder.Attention = value.ContactPerson; 
                }
                
                OnPropertyChanged(nameof(CurrentOrder));
                SubmitOrderCommand.NotifyCanExecuteChanged();
            }
        }

        private void OnMenuTabSelected(object? sender, string tabName)
        {
            switch (tabName)
            {
                case "Dashboard": WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Orders")); break;
                case "All Orders": WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("OrderList")); break;
                case "Inventory": WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Inventory")); break;
                case "ItemList": WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("ItemList")); break;
                case "Suppliers": WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Suppliers")); break;
                case "New Order": IsReadOnly = false; break;
            }
        }

        #endregion
    }
}
