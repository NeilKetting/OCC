using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.CustomerHub.ViewModels
{
    using OCC.WpfClient.Infrastructure.Exceptions;

    public partial class CustomerDetailViewModel : DetailViewModelBase
    {
        private readonly CustomerListViewModel _parent;
        private readonly ICustomerService _customerService;
        private readonly Customer _model;

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _header;
        [ObservableProperty] private string _email;
        [ObservableProperty] private string _phone;
        [ObservableProperty] private string _address;
        [ObservableProperty] private ObservableCollection<CustomerContact> _contacts;

        public bool IsNew => _model.Id == Guid.Empty;

        public CustomerDetailViewModel(
            CustomerListViewModel parent,
            Customer model,
            ICustomerService customerService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _parent = parent;
            _model = model;
            _customerService = customerService;

            Title = IsNew ? "New Customer" : $"Edit {model.Name}";
            
            _name = model.Name;
            _header = model.Header;
            _email = model.Email;
            _phone = model.Phone;
            _address = model.Address;
            _contacts = new ObservableCollection<CustomerContact>(model.Contacts ?? new List<CustomerContact>());
        }

        protected override async Task ExecuteSaveAsync()
        {
            _model.Name = Name;
            _model.Header = Header;
            _model.Email = Email;
            _model.Phone = Phone;
            _model.Address = Address;
            _model.Contacts = Contacts.ToList();

            if (IsNew)
            {
                await _customerService.CreateCustomerAsync(_model);
            }
            else
            {
                var success = await _customerService.UpdateCustomerAsync(_model);
                if (!success)
                {
                    throw new Exception("Failed to update customer. Please check your connection.");
                }
            }
        }

        protected override async Task<bool> ExecuteForceSaveAsync()
        {
            try
            {
                var latest = await _customerService.GetCustomerAsync(_model.Id);
                if (latest != null)
                {
                    _model.RowVersion = latest.RowVersion;
                    
                    // Sync the RowVersions of nested contacts so EF Core ignores their concurrency checks too.
                    foreach (var contact in _model.Contacts)
                    {
                        var latestContact = latest.Contacts.FirstOrDefault(c => c.Id == contact.Id);
                        if (latestContact != null)
                        {
                            contact.RowVersion = latestContact.RowVersion;
                        }
                    }
                    
                    var success = await _customerService.UpdateCustomerAsync(_model);
                    if (!success)
                    {
                        throw new System.Exception("Failed to force update customer. Please check your connection.");
                    }
                    return true;
                }
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error during force save");
                await _dialogService.ShowAlertAsync("Error", $"Failed to force save: {ex.Message}");
                return false;
            }
        }

        protected override async Task<bool> ValidateAsync()
        {
            ValidationErrors.Clear();
            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationErrors.Add("Customer name is required.");
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }
            HasErrors = false;
            return true;
        }

        protected override void OnSaveSuccess()
        {
            NotifySuccess("Success", $"Customer '{Name}' saved successfully.");
            _parent.LoadDataAsync().ConfigureAwait(false);
            _parent.CloseDetailView();
        }

        protected override async Task ExecuteReloadAsync()
        {
            var latest = await _customerService.GetCustomerAsync(_model.Id);
            if (latest != null)
            {
                // Update our model and properties
                _model.Name = latest.Name;
                _model.Header = latest.Header;
                _model.Email = latest.Email;
                _model.Phone = latest.Phone;
                _model.Address = latest.Address;
                _model.Contacts = latest.Contacts;
                _model.RowVersion = latest.RowVersion;

                Name = _model.Name;
                Header = _model.Header;
                Email = _model.Email;
                Phone = _model.Phone;
                Address = _model.Address;
                Contacts = new ObservableCollection<CustomerContact>(_model.Contacts ?? new List<CustomerContact>());
                
                Title = $"Edit {Name} (Reloaded)";
            }
        }

        protected override void OnCancel()
        {
            _parent.CloseDetailView();
        }

        [RelayCommand]
        private void AddContact()
        {
            Contacts.Add(new CustomerContact { Name = "", Department = "" });
        }

        [RelayCommand]
        private void RemoveContact(CustomerContact contact)
        {
            Contacts.Remove(contact);
        }

        protected override string GetReportTitle() => $"Customer Profile: {Name}";
        protected override object GetReportItem() => new
        {
            Name,
            Email,
            Phone,
            Address,
            ContactsCount = Contacts.Count,
            PrimaryContact = Contacts.FirstOrDefault()?.Name ?? "N/A"
        };
    }
}
