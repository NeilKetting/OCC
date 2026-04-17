using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Messages;
using OCC.Client.Services.Interfaces;
using OCC.Client.Models;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages; // For NavigationRequestMessage
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    public partial class RestockReviewViewModel : ViewModelBase
    {
        private readonly IOrderManager _orderManager;
        private readonly OrderStateService _orderStateService;



        [ObservableProperty]
        private ObservableCollection<SupplierRestockGroup> _groups = new();

        public OrderMenuViewModel OrderMenu { get; }

        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;

        public RestockReviewViewModel(IOrderManager orderManager, OrderStateService orderStateService, OrderMenuViewModel orderMenu, IAuthService authService, IDialogService dialogService)
        {
            _orderManager = orderManager;
            _orderStateService = orderStateService;
            OrderMenu = orderMenu;
            _authService = authService;
            _dialogService = dialogService;
            OrderMenu.TabSelected += OnMenuTabSelected;
        }

        private void OnMenuTabSelected(object? sender, string tabName)
        {
             switch (tabName)
            {
                case "Dashboard":
                    WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Orders"));
                    break;
                case "All Orders":
                    WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("OrderList"));
                    break;
                case "Inventory":
                    WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Inventory"));
                    break;
                case "ItemList":
                     WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("ItemList"));
                     break;
                case "Suppliers":
                    WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Suppliers"));
                    break;
                case "New Order":
                    WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("CreateOrder"));
                    break;
            }
        }

        public RestockReviewViewModel() 
        { 
            _orderManager = null!; 
            _orderStateService = null!; 
            OrderMenu = null!; 
            _authService = null!; 
            _dialogService = null!;
        }

        public async Task LoadData()
        {
            IsBusy = true;
            try
            {
                var branch = _authService?.CurrentUser?.Branch;
                var candidates = await _orderManager.GetRestockCandidatesAsync(branch);
                
                var grouped = candidates.GroupBy(c => c.Item.Supplier)
                                        .Select(g => new SupplierRestockGroup(g.Key, g.ToList()))
                                        .OrderByDescending(g => g.Items.Count)
                                        .ToList();

                Groups.Clear();
                foreach (var g in grouped) Groups.Add(g);
                
                if (!Groups.Any())
                {
                     WeakReferenceMessenger.Default.Send(new ToastNotificationMessage(new ToastMessage { Title = "Stock Status", Message = "All items are well stocked!", Type = ToastType.Success }));
                     Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task CreateOrder(SupplierRestockGroup group)
        {
            if (group == null) return;

            var firstItem = group.Items.FirstOrDefault();
            if (firstItem != null)
            {
                var userBranch = _authService?.CurrentUser?.Branch;
                if (userBranch != null && firstItem.TargetBranch != userBranch)
                {
                    bool confirm = await _dialogService.ShowConfirmationAsync(
                        "Cross-Branch Replenishment",
                        $"You are about to create an order for {firstItem.TargetBranch}, but you are logged into {userBranch}.\n\nProceed?");
                    
                    if (!confirm) return;
                }
            }

            // Async workaround:
            PrepareOrderAndNavigate(group);
        }

        private async void PrepareOrderAndNavigate(SupplierRestockGroup group)
        {
            IsBusy = true;
            try
            {
                var suppliers = await _orderManager.GetSuppliersAsync();
                var supplierObj = suppliers.FirstOrDefault(s => s.Name.Equals(group.SupplierName, StringComparison.OrdinalIgnoreCase));

                var order = _orderManager.CreateNewOrderTemplate(OrderType.PurchaseOrder);
                order.Notes = $"Restock for {group.SupplierName}";
                order.ExpectedDeliveryDate = DateTime.Today.AddDays(7); // Default
                
                if (supplierObj != null)
                {
                    order.SupplierId = supplierObj.Id;
                    order.SupplierName = supplierObj.Name;
                    order.EntityAddress = supplierObj.Address;
                    order.EntityTel = supplierObj.Phone;
                    order.Attention = supplierObj.ContactPerson;
                }
                else
                {
                    order.SupplierName = group.SupplierName;
                }

                // Set branch from the first candidate if available, else user's branch
                var firstCandidate = group.Items.FirstOrDefault();
                if (firstCandidate != null)
                {
                     order.Branch = firstCandidate.TargetBranch;
                }

                // Group by Item and Branch to prevent misallocation across warehouses
                var consolidatedItems = group.Items.GroupBy(c => new { c.Item.Id, c.TargetBranch })
                    .Select(g => 
                    {
                        var first = g.First();
                        var item = first.Item;
                        var branch = g.Key.TargetBranch;

                        // Target is now just the Threshold (no x2 multiplier)
                        double target = branch == Branch.JHB ? item.JhbReorderPoint : item.CptReorderPoint;
                        double currentHand = branch == Branch.JHB ? item.JhbQuantity : item.CptQuantity;
                        
                        // Sum on-order for this item/combination
                        double onOrder = g.Sum(c => c.QuantityOnOrder);
                        
                        // Ensure at least 1 is ordered if it's on the restock list
                        double needed = Math.Max(1, target - (currentHand + onOrder));
                        
                        // Only return lines that match the PO branch
                        if (branch != order.Branch) return null;

                        return new OrderLine
                        {
                            InventoryItemId = item.Id,
                            ItemCode = item.Sku,
                            Description = item.Description,
                            Category = item.Category,
                            UnitOfMeasure = item.UnitOfMeasure,
                            UnitPrice = item.AverageCost,
                            QuantityOrdered = needed,
                            LineTotal = (decimal)needed * item.AverageCost,
                            VatAmount = ((decimal)needed * item.AverageCost) * 0.15m
                        };
                    })
                    .Where(l => l != null);

                foreach (var line in consolidatedItems)
                {
                    if (line != null) order.Lines.Add(line);
                }

                _orderStateService.SaveState(order, null);
                WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("CreateOrder"));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Close()
        {
             WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Orders"));
        }
    }

    public class SupplierRestockGroup
    {
        public string SupplierName { get; }
        public ObservableCollection<RestockCandidateDto> Items { get; }

        public SupplierRestockGroup(string name, System.Collections.Generic.List<RestockCandidateDto> items)
        {
            SupplierName = string.IsNullOrEmpty(name) ? "Unknown Supplier" : name;
            Items = new ObservableCollection<RestockCandidateDto>(items);
        }
        
        // Helper for UI badges
        public int Count => Items.Count;
    }
}
