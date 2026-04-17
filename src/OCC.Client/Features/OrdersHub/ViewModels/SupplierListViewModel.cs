using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using OCC.Client.ViewModels.Core;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing a list of suppliers, including searching, editing, and dependency-checked deletion.
    /// </summary>
    /// <summary>
    /// ViewModel for managing a list of suppliers, including searching, editing, and dependency-checked deletion.
    /// </summary>
    public partial class SupplierListViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ISupplierImportService _importService;
        private readonly ILogger<SupplierListViewModel> _logger;
        private readonly IAuthService _authService;
        private List<SupplierSummaryDto> _allSuppliers = new();

        #endregion

        #region Observables

        /// <summary>
        /// Gets the collection of suppliers currently displayed in the list.
        /// </summary>
        public ObservableCollection<SupplierSummaryDto> Suppliers { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the ViewModel is busy with an asynchronous operation.
        /// </summary>


        /// <summary>
        /// Gets or sets the search query used to filter the supplier list by name or email.
        /// </summary>
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        /// <summary>
        /// Gets or sets the currently selected supplier in the list.
        /// </summary>
        [ObservableProperty]
        private SupplierSummaryDto? _selectedSupplier;

        #endregion

        #region Constructors

        public SupplierListViewModel(IOrderManager orderManager, IDialogService dialogService, ISupplierImportService importService, ILogger<SupplierListViewModel> logger, IAuthService authService)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _importService = importService;
            _logger = logger;
            _authService = authService;

            if (_authService.CurrentUser?.Branch != null)
            {
                _selectedBranchFilter = _authService.CurrentUser.Branch.Value.ToString();
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        public virtual async Task ImportSuppliers()
        {
            try
            {
                var filePath = await _dialogService.PickFileAsync("Select Supplier List CSV", new[] { "*.csv" });

                if (!string.IsNullOrEmpty(filePath))
                {
                    IsBusy = true;
                    BusyText = "Importing suppliers...";
                    
                    await using var stream = System.IO.File.OpenRead(filePath);
                    var (success, failed, errors) = await _importService.ImportSuppliersAsync(stream);

                    if (failed == 0)
                    {
                        await _dialogService.ShowAlertAsync("Success", $"Successfully imported {success} suppliers.");
                    }
                    else
                    {
                        var errorMsg = $"Imported: {success}\nSkipped/Failed: {failed}\n\nErrors:\n" + string.Join("\n", errors.Take(5));
                        if (errors.Count > 5) errorMsg += "\n...";
                        
                        await _dialogService.ShowAlertAsync("Import Result", errorMsg);
                    }
                    
                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error importing suppliers");
                 await _dialogService.ShowAlertAsync("Error", $"Import failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Command to initiate the process of adding a new supplier.
        /// </summary>
        [RelayCommand]
        public virtual void AddSupplier()
        {
            AddSupplierRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Command to initiate editing for a specific supplier.
        /// </summary>
        /// <param name="supplier">The supplier to be edited.</param>
        [RelayCommand]
        public virtual void EditSupplier(SupplierSummaryDto supplier)
        {
             if (supplier == null) return;
             EditSupplierRequested?.Invoke(this, supplier);
        }

        /// <summary>
        /// Command to permanently delete a supplier after performing a dependency check to ensure no orders are associated with them.
        /// </summary>
        /// <param name="supplier">The supplier to be deleted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [RelayCommand]
        public virtual async Task DeleteSupplier(SupplierSummaryDto supplier)
        {
             if (supplier == null) return;

             try 
             {
                 BusyText = "Checking dependencies...";
                 IsBusy = true;
                 
                 // Note: Ideally the server should handle the dependency check on delete.
                 // For now, we'll keep the client-side check but use the ID.
                 var allOrders = await _orderManager.GetOrdersAsync();
                 if (allOrders.Any(o => o.SupplierId == supplier.Id))
                 {
                     await _dialogService.ShowAlertAsync("Restricted", $"Cannot delete supplier '{supplier.Name}' because they have associated orders.");
                     return;
                 }
             }
             catch(Exception ex)
             {
                  _logger.LogError(ex, "Failed to check orders during supplier delete for {SupplierName}", supplier.Name);
                  await _dialogService.ShowAlertAsync("Error", "Could not verify dependencies. Delete operation aborted for safety.");
                  return;
             }
             finally
             {
                 IsBusy = false;
             }

             var confirm = await _dialogService.ShowConfirmationAsync("Confirm Delete", $"Are you sure you want to delete supplier '{supplier.Name}'? This action cannot be undone.");
              if (confirm)
              {
                  try
                  {
                      BusyText = $"Deleting {supplier.Name}...";
                      IsBusy = true;
                      await _orderManager.DeleteSupplierAsync(supplier.Id);
                      await LoadData();
                  }
                  catch(Exception ex)
                  {
                      await _dialogService.ShowAlertAsync("Error", $"Failed to delete supplier: {ex.Message}");
                  }
                  finally
                  {
                      IsBusy = false;
                  }
              }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Asynchronously loads all suppliers from the Order Manager and applies the current search filter.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task LoadData()
        {
            try
            {
                BusyText = "Loading suppliers...";
                IsBusy = true;
                _allSuppliers = (await _orderManager.GetSupplierSummariesAsync()).ToList();
                FilterSuppliers();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error loading suppliers");
                await _dialogService.ShowAlertAsync("Error", $"Failed to load suppliers: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Filters the full collection of suppliers based on the current search query and branch filter.
        /// </summary>
        private void FilterSuppliers()
        {
            Suppliers.Clear();
            
            var query = _allSuppliers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                query = query.Where(s => s.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) 
                                      || s.Email.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedBranchFilter != "All" && Enum.TryParse<Branch>(SelectedBranchFilter, out var branch))
            {
                 // Show Global suppliers (null) AND Branch specific suppliers
                 // OR should strictly filter?
                 // "split Supplier for cpt and jhb" implies strict separation usually, but global suppliers exist.
                 // I'll include null branch suppliers always? Or only if filtering for "All"?
                 // Creating Order logic: `s.Branch == null || s.Branch == targetBranch`
                 // So here: `s.Branch == null || s.Branch == branch`
                 query = query.Where(s => s.Branch == null || s.Branch == branch.ToString());
            }

            foreach (var s in query.OrderBy(s => s.Name))
            {
                Suppliers.Add(s);
            }
        }

        [ObservableProperty]
        private string _selectedBranchFilter = "All";
        
        public List<string> BranchOptions { get; } = new List<string> { "All" }.Concat(Enum.GetNames(typeof(Branch))).ToList();

        partial void OnSelectedBranchFilterChanged(string value) => FilterSuppliers();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Event raised when the user requests to add a new supplier.
        /// </summary>
        public event EventHandler? AddSupplierRequested;

        /// <summary>
        /// Event raised when the user requests to edit an existing supplier.
        /// </summary>
        public event EventHandler<SupplierSummaryDto>? EditSupplierRequested;

        /// <summary>
        /// Handles changes to the search query by reapplying the supplier filter.
        /// </summary>
        partial void OnSearchQueryChanged(string value) => FilterSuppliers();

        #endregion
    }
}
