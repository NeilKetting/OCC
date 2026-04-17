using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.OrdersHub.ViewModels
{
    /// <summary>
    /// ViewModel for adding or editing supplier details, including bank information mapping.
    /// </summary>
    /// <summary>
    /// ViewModel for adding or editing supplier details, including bank information mapping.
    /// </summary>
    public partial class SupplierDetailViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IOrderManager _orderManager;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SupplierDetailViewModel> _logger;

        #endregion

        #region Observables

        /// <summary>
        /// Gets or sets the supplier being edited or created.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private Supplier _supplier = new();

        public new string Title
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Supplier?.Name))
                {
                    return IsEditMode ? "Edit Supplier" : "New Supplier";
                }
                return Supplier.Name;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the view is in edit mode (true) or add mode (false).
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// Gets or sets a value indicating whether an asynchronous operation is in progress.
        /// </summary>


        /// <summary>
        /// Gets or sets the bank selected from the predefined list.
        /// </summary>
        [ObservableProperty]
        private OCC.Shared.Models.BankName _selectedBank = OCC.Shared.Models.BankName.None;

        /// <summary>
        /// Gets or sets the custom bank name if "Other" is selected.
        /// </summary>
        [ObservableProperty]
        private string _customBankName = string.Empty;

        /// <summary>
        /// Gets a value indicating whether a custom bank name entry is required.
        /// </summary>
        public bool IsOtherBankSelected => SelectedBank == OCC.Shared.Models.BankName.Other;

        /// <summary>
        /// Gets the list of available bank options.
        /// </summary>
        public OCC.Shared.Models.BankName[] AvailableBanks { get; } = Enum.GetValues<OCC.Shared.Models.BankName>();

        /// <summary>
        /// Gets the list of available branches.
        /// </summary>
        public OCC.Shared.Models.Branch[] AvailableBranches { get; } = Enum.GetValues<OCC.Shared.Models.Branch>();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SupplierDetailViewModel"/> class with required dependencies.
        /// </summary>
        /// <param name="orderManager">Central manager for supplier operations.</param>
        /// <param name="dialogService">Service for user notifications.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public SupplierDetailViewModel(IOrderManager orderManager, IDialogService dialogService, ILogger<SupplierDetailViewModel> logger)
        {
            _orderManager = orderManager;
            _dialogService = dialogService;
            _logger = logger;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to set the bank selection.
        /// </summary>
        /// <param name="bank">The bank to select.</param>
        [RelayCommand]
        private void SetSelectedBank(OCC.Shared.Models.BankName bank)
        {
            SelectedBank = bank;
        }

        /// <summary>
        /// Command to save the current supplier details to the database via the Order Manager.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [RelayCommand]
        public async Task Save()
        {
            if (string.IsNullOrWhiteSpace(Supplier.Name))
            {
                await _dialogService.ShowAlertAsync("Validation", "Supplier Name is required.");
                return; 
            }

            if (SelectedBank == OCC.Shared.Models.BankName.None)
            {
                 Supplier.BankName = string.Empty;
            }
            else
            {
                 Supplier.BankName = IsOtherBankSelected ? CustomBankName : GetEnumDescription(SelectedBank);
            }

            try
            {
                BusyText = "Saving supplier details...";
                IsBusy = true;
                
                if (IsEditMode)
                {
                    await _orderManager.UpdateSupplierAsync(Supplier);
                }
                else
                {
                    await _orderManager.CreateSupplierAsync(Supplier);
                }
                
                Saved?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch(Exception ex)
            {
                 _logger.LogError(ex, "Error saving supplier {SupplierName}", Supplier.Name);
                 await _dialogService.ShowAlertAsync("Error", "An unexpected error occurred while saving. Please try again.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Command to cancel the current operation and close the view.
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads a supplier for editing or initializes a new supplier for creation.
        /// Maps internal bank enum values to display strings.
        /// </summary>
        /// <param name="supplier">The supplier to load, or null for a new supplier.</param>
        public void Load(Supplier? supplier = null)
        {
            if (supplier != null)
            {
                Supplier = new Supplier
                {
                    Id = supplier.Id,
                    Name = supplier.Name,
                    ContactPerson = supplier.ContactPerson,
                    Email = supplier.Email,
                    Phone = supplier.Phone,
                    Address = supplier.Address,
                    VatNumber = supplier.VatNumber,
                    BankAccountNumber = supplier.BankAccountNumber,
                    SupplierAccountNumber = supplier.SupplierAccountNumber,
                    BranchCode = supplier.BranchCode,
                    Branch = supplier.Branch
                };
                IsEditMode = true;

                var dbBankName = supplier.BankName;
                var matched = false;
                
                if (!string.IsNullOrEmpty(dbBankName))
                {
                    foreach (var bank in AvailableBanks)
                    {
                        if (bank == OCC.Shared.Models.BankName.None || bank == OCC.Shared.Models.BankName.Other) continue;

                        if (GetEnumDescription(bank).Equals(dbBankName, StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedBank = bank;
                            matched = true;
                            break;
                        }
                    }
                }

                if (!matched && !string.IsNullOrEmpty(dbBankName))
                {
                    SelectedBank = OCC.Shared.Models.BankName.Other;
                    CustomBankName = dbBankName;
                }
                else if (!matched)
                {
                    SelectedBank = OCC.Shared.Models.BankName.None;
                    CustomBankName = string.Empty;
                }
            }
            else
            {
                Supplier = new Supplier();
                IsEditMode = false;
                SelectedBank = OCC.Shared.Models.BankName.None;
                CustomBankName = string.Empty;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Event raised when the user requests to close the view.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Event raised after a supplier has been successfully saved.
        /// </summary>
        public event EventHandler? Saved;

        /// <summary>
        /// Notifies when the bank selection changes to update visibility of the custom bank name field.
        /// </summary>
        partial void OnSelectedBankChanged(OCC.Shared.Models.BankName value)
        {
             OnPropertyChanged(nameof(IsOtherBankSelected));
        }

        /// <summary>
        /// Helper to retrieve the description attribute of an enum value.
        /// </summary>
        private string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (System.ComponentModel.DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(System.ComponentModel.DescriptionAttribute));
            return attribute?.Description ?? value.ToString();
        }

        #endregion
    }
}
