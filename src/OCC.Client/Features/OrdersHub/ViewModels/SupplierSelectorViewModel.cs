using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
    public partial class SupplierSelectorViewModel : ViewModelBase
    {
        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SupplierSelectorViewModel> _logger;

        private List<Supplier> _allSuppliersMaster = new();

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private bool _isAddingNew;

        [ObservableProperty]
        private string _newSupplierName = string.Empty;

        public ObservableCollection<Supplier> FilteredSuppliers { get; } = new();

        public SupplierSelectorViewModel() 
        {
            _orderManager = null!;
            _dialogService = null!;
            _logger = null!;

            // Design-time data
            FilteredSuppliers.Add(new Supplier { Name = "Sample Supplier 1", ContactPerson = "John Doe" });
            FilteredSuppliers.Add(new Supplier { Name = "Sample Supplier 2", ContactPerson = "Jane Smith" });
        }

        public SupplierSelectorViewModel(
            IOrderManager orderManager,
            IDialogService dialogService,
            ILogger<SupplierSelectorViewModel> logger)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _logger = logger;
        }

        public virtual void Initialize(IEnumerable<Supplier> suppliers, Branch currentBranch)
        {
            _allSuppliersMaster = suppliers.ToList();
            Filter(currentBranch);
        }

        public virtual void Filter(Branch branch)
        {
            var filtered = _allSuppliersMaster
                .Where(s => s.Branch == null || s.Branch == branch)
                .OrderBy(x => x.Name)
                .ToList();
            
            var currentSelection = SelectedSupplier;
            
            FilteredSuppliers.Clear();
            foreach(var s in filtered) FilteredSuppliers.Add(s);

            // Retain selection if still valid for this branch
            if (currentSelection != null)
            {
                if (FilteredSuppliers.Contains(currentSelection))
                {
                    SelectedSupplier = currentSelection;
                }
                else if (currentSelection.Branch != null && currentSelection.Branch != branch)
                {
                    SelectedSupplier = null;
                }
            }
        }

        [RelayCommand]
        public void ToggleQuickAdd()
        {
            IsAddingNew = !IsAddingNew;
            if (IsAddingNew) NewSupplierName = "";
        }

        [RelayCommand]
        public async Task QuickCreateSupplier()
        {
            if (string.IsNullOrWhiteSpace(NewSupplierName)) return;
            
            try
            {
                 var created = await _orderManager.QuickCreateSupplierAsync(NewSupplierName);
                 _allSuppliersMaster.Add(created);
                 
                 // We don't know the branch filter state here easily without passing it, 
                 // but typically newly created suppliers are global or for current branch.
                 // For now, just re-filter if we have a way to know the branch, or just add it.
                 FilteredSuppliers.Add(created); 
                 SelectedSupplier = created;
                 IsAddingNew = false;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to quick create supplier");
                await _dialogService.ShowAlertAsync("Error", "Failed to create supplier.");
            }
        }
    }
}
