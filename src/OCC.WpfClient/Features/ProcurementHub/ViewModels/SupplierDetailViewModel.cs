using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Exceptions;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels
{
    public partial class SupplierDetailViewModel : DetailViewModelBase
    {
        
        private readonly ISupplierService _supplierService;
        private readonly Supplier _model;

        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _city = string.Empty;
        [ObservableProperty] private string _postalCode = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _contactPerson = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _vatNumber = string.Empty;
        [ObservableProperty] private string _bankAccountNumber = string.Empty;
        [ObservableProperty] private string _branchCode = string.Empty;
        [ObservableProperty] private string _supplierAccountNumber = string.Empty;
        [ObservableProperty] private Branch? _selectedBranch;

        [ObservableProperty] private BankName _selectedBank = BankName.None;
        [ObservableProperty] private string _customBankName = string.Empty;
        [ObservableProperty] private ObservableCollection<SupplierContact> _contacts = new();

        public bool IsOtherBankSelected => SelectedBank == BankName.Other;
        public List<BankName> AvailableBanks { get; } = Enum.GetValues<BankName>().ToList();
        public List<Branch?> AvailableBranches { get; } = new List<Branch?> { null }.Concat(Enum.GetValues<Branch>().Cast<Branch?>()).ToList();

        public bool IsNew => _model.Id == Guid.Empty;

        public SupplierDetailViewModel(
            Supplier model,
            ISupplierService supplierService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _model = model;
            _supplierService = supplierService;

            Title = IsNew ? "New Supplier" : $"Edit {model.Name}";

            InitializeFromModel(model);
        }

        private void InitializeFromModel(Supplier model)
        {
            Name = model.Name;
            Address = model.Address;
            City = model.City;
            PostalCode = model.PostalCode;
            Phone = model.Phone;
            ContactPerson = model.ContactPerson;
            Email = model.Email;
            VatNumber = model.VatNumber;
            BankAccountNumber = model.BankAccountNumber;
            BranchCode = model.BranchCode;
            SupplierAccountNumber = model.SupplierAccountNumber;
            SelectedBranch = model.Branch;

            Contacts = new ObservableCollection<SupplierContact>(model.Contacts ?? new List<SupplierContact>());
            if (Contacts.Count == 0 && (!string.IsNullOrEmpty(model.ContactPerson) || !string.IsNullOrEmpty(model.Email) || !string.IsNullOrEmpty(model.Phone)))
            {
                Contacts.Add(new SupplierContact
                {
                    Id = Guid.NewGuid(),
                    SupplierId = model.Id,
                    ContactName = model.ContactPerson,
                    Email = model.Email,
                    Phone = model.Phone,
                    Department = "General"
                });
            }

            // Map BankName string to Enum
            if (!string.IsNullOrEmpty(model.BankName))
            {
                var matched = false;
                foreach (var bank in AvailableBanks)
                {
                    if (bank == BankName.None || bank == BankName.Other) continue;

                    if (GetEnumDescription(bank).Equals(model.BankName, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedBank = bank;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    SelectedBank = BankName.Other;
                    CustomBankName = model.BankName;
                }
            }
            else
            {
                SelectedBank = BankName.None;
                CustomBankName = string.Empty;
            }
        }

        [RelayCommand]
        private void AddContact()
        {
            Contacts.Add(new SupplierContact
            {
                Id = Guid.NewGuid(),
                SupplierId = _model.Id,
                ContactName = string.Empty,
                Email = string.Empty,
                Phone = string.Empty,
                Department = "Sales"
            });
        }

        [RelayCommand]
        private void RemoveContact(SupplierContact contact)
        {
            if (contact != null)
            {
                Contacts.Remove(contact);
            }
        }

        partial void OnSelectedBankChanged(BankName value)
        {
            OnPropertyChanged(nameof(IsOtherBankSelected));
        }

        protected override async Task ExecuteSaveAsync()
        {
            UpdateModelFromProperties();

            if (IsNew)
            {
                await _supplierService.CreateSupplierAsync(_model);
            }
            else
            {
                await _supplierService.UpdateSupplierAsync(_model);
            }
        }

        protected override async Task<bool> ExecuteForceSaveAsync()
        {
            try
            {
                var latest = await _supplierService.GetSupplierAsync(_model.Id);
                if (latest != null)
                {
                    _model.RowVersion = latest.RowVersion;
                    UpdateModelFromProperties();
                    await _supplierService.UpdateSupplierAsync(_model);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during force save");
                await _dialogService.ShowAlertAsync("Error", $"Failed to force save: {ex.Message}");
                return false;
            }
        }

        private void UpdateModelFromProperties()
        {
            _model.Name = Name;
            _model.Address = Address;
            _model.City = City;
            _model.PostalCode = PostalCode;
            _model.VatNumber = VatNumber;
            _model.BankAccountNumber = BankAccountNumber;
            _model.BranchCode = BranchCode;
            _model.SupplierAccountNumber = SupplierAccountNumber;
            _model.Branch = SelectedBranch;
            _model.Contacts = Contacts.ToList();

            if (Contacts.Count > 0)
            {
                var primary = Contacts[0];
                _model.ContactPerson = primary.ContactName;
                _model.Email = string.Join("; ", Contacts.Where(c => !string.IsNullOrWhiteSpace(c.Email)).Select(c => c.Email.Trim()).Distinct());
                _model.Phone = primary.Phone;
            }
            else
            {
                _model.ContactPerson = ContactPerson;
                _model.Email = Email;
                _model.Phone = Phone;
            }

            if (SelectedBank == BankName.None)
            {
                _model.BankName = string.Empty;
            }
            else
            {
                _model.BankName = IsOtherBankSelected ? CustomBankName : GetEnumDescription(SelectedBank);
            }
        }

        protected override async Task<bool> ValidateAsync()
        {
            ValidationErrors.Clear();
            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationErrors.Add("Supplier name is required.");
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }
            HasErrors = false;
            return true;
        }

        protected override void OnSaveSuccess()
        {
            NotifySuccess("Success", $"Supplier '{Name}' saved successfully.");
            base.OnSaveSuccess();
        }

        protected override async Task ExecuteReloadAsync()
        {
            var latest = await _supplierService.GetSupplierAsync(_model.Id);
            if (latest != null)
            {
                _model.RowVersion = latest.RowVersion;
                InitializeFromModel(latest);
                Title = $"Edit {Name} (Reloaded)";
            }
        }

        protected override void OnCancel()
        {
            base.OnCancel();
        }

        private string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        protected override string GetReportTitle() => $"Supplier Profile: {Name}";
        protected override object GetReportItem() => new
        {
            Name,
            ContactPerson,
            Email,
            Phone,
            Address,
            City,
            PostalCode,
            VatNumber,
            BankName = _model.BankName,
            BankAccountNumber,
            BranchCode
        };
    }
}
