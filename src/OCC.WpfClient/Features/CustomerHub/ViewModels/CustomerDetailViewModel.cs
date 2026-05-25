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
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.CustomerHub.ViewModels
{
    using OCC.WpfClient.Infrastructure.Exceptions;

    public partial class CustomerDetailViewModel : DetailViewModelBase
    {
        
        private readonly ICustomerService _customerService;
        private readonly Customer _model;
        private readonly ConnectionSettings _connectionSettings;

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _header;
        [ObservableProperty] private string _email;
        [ObservableProperty] private string _phone;
        [ObservableProperty] private string _address;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullLogoUrl))]
        private string _logoUrl = string.Empty;
        [ObservableProperty] private ObservableCollection<CustomerContact> _contacts;

        public bool IsNew => _model.Id == Guid.Empty;

        public string? FullLogoUrl
        {
            get
            {
                if (string.IsNullOrEmpty(LogoUrl)) return null;
                if (LogoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return LogoUrl;
                var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
                return $"{baseUrl.TrimEnd('/')}/{LogoUrl.TrimStart('/')}";
            }
        }

        public CustomerDetailViewModel(
            Customer model,
            ICustomerService customerService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService,
            ConnectionSettings connectionSettings) : base(dialogService, logger, pdfService)
        {
            _model = model;
            _customerService = customerService;
            _connectionSettings = connectionSettings;

            Title = IsNew ? "New Customer" : $"Edit {model.Name}";
            
            _name = model.Name;
            _header = model.Header;
            _email = model.Email;
            _phone = model.Phone;
            _address = model.Address;
            _logoUrl = model.LogoUrl ?? string.Empty;
            _contacts = new ObservableCollection<CustomerContact>(model.Contacts ?? new List<CustomerContact>());
        }

        protected override async Task ExecuteSaveAsync()
        {
            _model.Name = Name;
            _model.Header = Header;
            _model.Email = Email;
            _model.Phone = Phone;
            _model.Address = Address;
            _model.LogoUrl = LogoUrl;
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
            base.OnSaveSuccess();
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
                _model.LogoUrl = latest.LogoUrl;
                _model.Contacts = latest.Contacts;
                _model.RowVersion = latest.RowVersion;

                Name = _model.Name;
                Header = _model.Header;
                Email = _model.Email;
                Phone = _model.Phone;
                Address = _model.Address;
                LogoUrl = _model.LogoUrl ?? string.Empty;
                Contacts = new ObservableCollection<CustomerContact>(_model.Contacts ?? new List<CustomerContact>());
                
                Title = $"Edit {Name} (Reloaded)";
            }
        }

        protected override void OnCancel()
        {
            base.OnCancel();
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

        [RelayCommand]
        private async Task UploadLogoAsync()
        {
            if (IsNew)
            {
                await _dialogService.ShowAlertAsync("Save Required", "Please save the customer first before uploading a logo.");
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Customer Logo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    IsBusy = true;
                    BusyText = "Uploading logo...";

                    var logoPath = dialog.FileName;
                    var uploadedUrl = await _customerService.UploadLogoAsync(_model.Id, logoPath);
                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        LogoUrl = uploadedUrl;
                        _model.LogoUrl = uploadedUrl; // Sync with model
                        NotifySuccess("Success", "Logo uploaded successfully.");
                    }
                    else
                    {
                        NotifyError("Error", "Failed to upload logo.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload logo");
                    NotifyError("Error", $"Failed to upload logo: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task RemoveLogoAsync()
        {
            if (IsNew) return;

            try
            {
                IsBusy = true;
                BusyText = "Removing logo...";

                _model.LogoUrl = string.Empty;
                var success = await _customerService.UpdateCustomerAsync(_model);
                if (success)
                {
                    LogoUrl = string.Empty;
                    NotifySuccess("Success", "Logo removed successfully.");
                }
                else
                {
                    NotifyError("Error", "Failed to remove logo.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove logo");
                NotifyError("Error", $"Failed to remove logo: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
