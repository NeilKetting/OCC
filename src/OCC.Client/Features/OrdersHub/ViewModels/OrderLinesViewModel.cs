using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Client.ModelWrappers;
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
    public partial class OrderLinesViewModel : ViewModelBase
    {
        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ILogger<OrderLinesViewModel> _logger;
        private readonly IOrderCalculationService _calculationService;

        [ObservableProperty]
        private OrderWrapper _currentOrder = null!;

        [ObservableProperty]
        private OrderLineWrapper _newLine = new(new OrderLine());

        [ObservableProperty]
        private InventoryItem? _selectedInventoryItem;

        [ObservableProperty]
        private bool _isReadOnly;

        [ObservableProperty]
        private string _productSearchText = string.Empty;

        public IEnumerable<InventoryItem> FilteredInventoryItemsByName => 
            string.IsNullOrWhiteSpace(ProductSearchText) 
                ? AllInventoryItems 
                : AllInventoryItems.Where(x => (x.Description?.Contains(ProductSearchText, StringComparison.OrdinalIgnoreCase) ?? false) || 
                                              (x.Sku?.Contains(ProductSearchText, StringComparison.OrdinalIgnoreCase) ?? false));

        public ObservableCollection<InventoryItem> AllInventoryItems { get; } = new();
        public ObservableCollection<string> AvailableUOMs { get; } = new();


        public OrderLinesViewModel() 
        {
            _orderManager = null!;
            _dialogService = null!;
            _logger = null!;
            _calculationService = null!;

            // Design-time data
            AvailableUOMs.Add("ea");
            AvailableUOMs.Add("m");
            
            CurrentOrder = new OrderWrapper(new Order());
            CurrentOrder.Lines.Add(new OrderLineWrapper(new OrderLine { Description = "Design Time Item", QuantityOrdered = 1, UnitPrice = 100, LineTotal = 100 }));
        }

        public OrderLinesViewModel(
            IOrderManager orderManager,
            IDialogService dialogService,
            ILogger<OrderLinesViewModel> logger,
            IOrderCalculationService calculationService)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _logger = logger;
            _calculationService = calculationService;
            
            AvailableUOMs.Add("ea");
            AvailableUOMs.Add("m");
            AvailableUOMs.Add("kg");
            AvailableUOMs.Add("L");
            AvailableUOMs.Add("unit");
        }

        public virtual void Initialize(OrderWrapper order, IEnumerable<InventoryItem> inventory)
        {
            CurrentOrder = order;
            SetupLineListeners();
            EnsureBlankRow();
        }

        #region Line Management

        public void SetupLineListeners()
        {
            CurrentOrder.Lines.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                    foreach (OrderLineWrapper item in e.NewItems) item.PropertyChanged += OnLineChanged;

                if (e.OldItems != null)
                    foreach (OrderLineWrapper item in e.OldItems) item.PropertyChanged -= OnLineChanged;
            };

            foreach (var line in CurrentOrder.Lines)
            {
                line.PropertyChanged += OnLineChanged;
            }
        }

        private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderLineWrapper.LineTotal) ||
                e.PropertyName == nameof(OrderLineWrapper.ItemCode) ||
                e.PropertyName == nameof(OrderLineWrapper.Description))
            {
                if (sender is OrderLineWrapper line)
                {
                    CheckForNewRow(line);
                }
            }
        }

        private void CheckForNewRow(OrderLineWrapper changedItem)
        {
            if (CurrentOrder?.Lines == null || IsReadOnly) return;

            var lastItem = CurrentOrder.Lines.LastOrDefault();
            if (lastItem == changedItem)
            {
                bool hasProduct = !string.IsNullOrWhiteSpace(lastItem.ItemCode);
                bool hasDesc = !string.IsNullOrWhiteSpace(lastItem.Description);
                bool hasPrice = lastItem.LineTotal > 0;

                if (hasProduct || hasDesc || hasPrice)
                {
                    EnsureBlankRow();
                }
            }
        }

        public void EnsureBlankRow()
        {
            if (CurrentOrder == null || IsReadOnly) return;

            var lastItem = CurrentOrder.Lines.LastOrDefault();
            bool isLastEmpty = lastItem == null ||
                              (string.IsNullOrWhiteSpace(lastItem.ItemCode) &&
                               string.IsNullOrWhiteSpace(lastItem.Description) &&
                               lastItem.LineTotal == 0);

            if (CurrentOrder.Lines.Count == 0 || !isLastEmpty)
            {
                Dispatcher.UIThread.Post(() => {
                    if (CurrentOrder.Lines.Count == 0 ||
                       !(CurrentOrder.Lines.LastOrDefault() is OrderLineWrapper last &&
                         string.IsNullOrWhiteSpace(last.ItemCode) &&
                         string.IsNullOrWhiteSpace(last.Description) &&
                         last.LineTotal == 0))
                    {
                        var newLine = new OrderLineWrapper(new OrderLine { UnitOfMeasure = "ea" });
                        newLine.PropertyChanged += OnLineChanged;
                        CurrentOrder.Lines.Add(newLine);
                    }
                });
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        public async Task AddLine()
        {
            if (string.IsNullOrWhiteSpace(NewLine.Description) && SelectedInventoryItem == null) return;

            // Use the calculation service
            var (net, vat) = _calculationService.CalculateLineTotals(NewLine.QuantityOrdered, NewLine.UnitPrice, CurrentOrder.TaxRate);
            NewLine.LineTotal = net;
            NewLine.VatAmount = vat;

            if (SelectedInventoryItem != null && SelectedInventoryItem.AverageCost > 0)
            {
                decimal threshold = SelectedInventoryItem.AverageCost * 1.10m;
                if (NewLine.UnitPrice > threshold)
                {
                    decimal pctIncrease = ((NewLine.UnitPrice - SelectedInventoryItem.AverageCost) / SelectedInventoryItem.AverageCost) * 100m;
                    bool confirm = await _dialogService.ShowConfirmationAsync(
                        "Price Increase Warning",
                        $"The price of R{NewLine.UnitPrice:F2} is {pctIncrease:F0}% higher than average.\n\nAccept and update average cost?");

                    if (confirm)
                    {
                        SelectedInventoryItem.AverageCost = NewLine.UnitPrice;
                        try { await _orderManager.UpdateInventoryItemAsync(SelectedInventoryItem); }
                        catch (Exception ex) { _logger.LogError(ex, "Failed to update avg cost"); }
                    }
                }
            }

            NewLine.CommitToModel();

            bool added = false;
            for (int i = 0; i < CurrentOrder.Lines.Count; i++)
            {
                var line = CurrentOrder.Lines[i];
                if (string.IsNullOrWhiteSpace(line.ItemCode) && string.IsNullOrWhiteSpace(line.Description) && line.QuantityOrdered == 0)
                {
                    CurrentOrder.Lines[i] = NewLine;
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                CurrentOrder.Lines.Insert(0, NewLine);
            }

            NewLine = new OrderLineWrapper(new OrderLine { QuantityOrdered = 1 });
        }

        [RelayCommand]
        public void RemoveLine(object item)
        {
            if (item is OrderLineWrapper line)
            {
                CurrentOrder.Lines.Remove(line);
                EnsureBlankRow();
            }
        }

        [RelayCommand]
        public void InsertLine(object parameter)
        {
            if (parameter is OrderLineWrapper anchor && CurrentOrder?.Lines != null)
            {
                var index = CurrentOrder.Lines.IndexOf(anchor);
                if (index >= 0)
                {
                    var newLine = new OrderLineWrapper(new OrderLine { QuantityOrdered = 1, UnitOfMeasure = "ea" });
                    newLine.PropertyChanged += OnLineChanged;
                    CurrentOrder.Lines.Insert(index, newLine);
                }
            }
        }

        [RelayCommand]
        public void AddEmptyLine()
        {
            var line = new OrderLineWrapper(new OrderLine { QuantityOrdered = 1, UnitOfMeasure = "ea" });
            line.PropertyChanged += OnLineChanged;
            CurrentOrder.Lines.Add(line);
        }

        public void UpdateLineFromSelection(OrderLineWrapper line, InventoryItem item)
        {
            if (line == null || item == null) return;

            line.ItemCode = item.Sku ?? "";
            line.Description = item.Description ?? "";
            line.UnitOfMeasure = item.UnitOfMeasure ?? "ea";
            line.UnitPrice = item.Price > 0 ? item.Price : (item.AverageCost > 0 ? item.AverageCost : 0);
            line.InventoryItemId = item.Id;

            var (net, vat) = _calculationService.CalculateLineTotals(line.QuantityOrdered, line.UnitPrice, CurrentOrder.TaxRate);
            line.LineTotal = net;
            line.VatAmount = vat;
        }

        #endregion

        #region Search

        public void TryCommitAutoSelection(OrderLineWrapper line)
        {
            if (SelectedInventoryItem != null || line == null || string.IsNullOrWhiteSpace(ProductSearchText)) return;

            var bestMatch = FilteredInventoryItemsByName.FirstOrDefault();
            if (bestMatch != null)
            {
                SelectedInventoryItem = bestMatch;
                UpdateLineFromSelection(line, bestMatch);
                
                ProductSearchText = string.Empty;
            }
        }

        private void SyncInventory()
        {
            AllInventoryItems.Clear();
            foreach (var item in _allInventoryMaster) AllInventoryItems.Add(item);
        }

        [RelayCommand]
        public void ClearProductSearch()
        {
            // Reset logic if needed, but per-row filtering is now handled by the UI
        }

        #endregion

        private IEnumerable<InventoryItem> _allInventoryMaster = Enumerable.Empty<InventoryItem>();

        public void SetInventoryMaster(IEnumerable<InventoryItem> inventory)
        {
            _allInventoryMaster = inventory;
            SyncInventory();
        }
    }
}
