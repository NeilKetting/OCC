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
        
        #region Private Fields

        // Database/API client service for customer resources
        private readonly ICustomerService _customerService;

        // The backing Domain Model representing the Customer details
        private readonly Customer _model;

        // Server connection parameters, including ApiBaseUrl
        private readonly ConnectionSettings _connectionSettings;

        #endregion

        #region Properties & Observables

        // Customer's trading or brand name
        [ObservableProperty]
        private string _name;

        // Summary statement or sub-heading for the customer profile
        [ObservableProperty]
        private string _header;

        // Primary email address for communication
        [ObservableProperty]
        private string _email;

        // Primary telephone number
        [ObservableProperty]
        private string _phone;

        // Corporate or physical street address
        [ObservableProperty]
        private string _address;

        // Relative path or full URL to the customer logo image
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FullLogoUrl))]
        private string _logoUrl = string.Empty;

        // Collection of individuals associated with this customer
        [ObservableProperty]
        private ObservableCollection<CustomerContact> _contacts;

        // Indicates if this is a newly created customer record
        public bool IsNew => _model.Id == Guid.Empty;

        // Computes the absolute URL path to retrieve the customer logo
        public string? FullLogoUrl
        {
            get
            {
                if (string.IsNullOrEmpty(LogoUrl)) return null;
                if (LogoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return LogoUrl;
                
                var baseUrl = _connectionSettings.ApiBaseUrl;
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    _logger.LogWarning("API Base URL is not configured. Customer logo URL may be invalid.");
                    return null;
                }
                
                return $"{baseUrl.TrimEnd('/')}/{LogoUrl.TrimStart('/')}";
            }
        }

        /// <summary>
        /// Compares the current view model properties and nested contacts collection against
        /// the values stored in the original backing model to determine if there are unsaved changes.
        /// </summary>
        public override bool IsDirty
        {
            get
            {
                // Check simple text properties for changes
                if ((Name ?? string.Empty) != (_model.Name ?? string.Empty)) return true;
                if ((Header ?? string.Empty) != (_model.Header ?? string.Empty)) return true;
                if ((Email ?? string.Empty) != (_model.Email ?? string.Empty)) return true;
                if ((Phone ?? string.Empty) != (_model.Phone ?? string.Empty)) return true;
                if ((Address ?? string.Empty) != (_model.Address ?? string.Empty)) return true;
                if ((LogoUrl ?? string.Empty) != (_model.LogoUrl ?? string.Empty)) return true;

                // Check nested contacts collection changes
                var originalContacts = (_model.Contacts ?? new List<CustomerContact>()).ToList();
                if (Contacts.Count != originalContacts.Count) return true;

                for (int i = 0; i < Contacts.Count; i++)
                {
                    var currentContact = Contacts[i];
                    var originalContact = originalContacts[i];
                    if (currentContact.Name != originalContact.Name) return true;
                    if (currentContact.Department != originalContact.Department) return true;
                    if (currentContact.Email != originalContact.Email) return true;
                    if (currentContact.Phone != originalContact.Phone) return true;
                }

                return false;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes detail editing context for the specified Customer.
        /// </summary>
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

        #endregion

        #region Commands

        /// <summary>
        /// Appends a new blank contact slot to the contacts collection.
        /// </summary>
        [RelayCommand]
        private void AddContact()
        {
            Contacts.Add(new CustomerContact { Name = "", Department = "" });
        }

        /// <summary>
        /// Removes the specified contact from the customer's contact list.
        /// </summary>
        [RelayCommand]
        private void RemoveContact(CustomerContact contact)
        {
            Contacts.Remove(contact);
        }

        /// <summary>
        /// Prompts the user to select an image from local storage and uploads it to the server.
        /// </summary>
        [RelayCommand]
        private async Task UploadLogoAsync()
        {
            if (IsNew)
            {
                await _dialogService.ShowAlertAsync("Save Required", "Please save the customer first before uploading a logo.");
                return;
            }

            var logoPath = _dialogService.ShowOpenFileDialog("Image Files|*.jpg;*.jpeg;*.png;*.bmp", "Select Customer Logo");
            if (!string.IsNullOrEmpty(logoPath))
            {
                try
                {
                    IsBusy = true;
                    BusyText = "Uploading logo...";

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

        /// <summary>
        /// Detaches and clears the customer's uploaded logo on the server.
        /// </summary>
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

        #endregion

        #region Methods

        /// <summary>
        /// Validates the customer record, checking mandatory name and correct format for email and phone fields.
        /// </summary>
        protected override async Task<bool> ValidateAsync()
        {
            ValidationErrors.Clear();

            // Name is mandatory
            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationErrors.Add("Customer name is required.");
            }

            // Email validation (only if provided)
            if (!string.IsNullOrWhiteSpace(Email))
            {
                var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(Email, emailPattern))
                {
                    ValidationErrors.Add("Invalid email address format.");
                }
            }

            // Phone validation (only if provided)
            if (!string.IsNullOrWhiteSpace(Phone))
            {
                var phonePattern = @"^\+?[0-9\s\-]{7,15}$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(Phone, phonePattern))
                {
                    ValidationErrors.Add("Invalid phone number format.");
                }
            }

            if (ValidationErrors.Any())
            {
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }

            HasErrors = false;
            return true;
        }

        /// <summary>
        /// Commits changes to the backend (creating a new customer or updating an existing one).
        /// </summary>
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

        /// <summary>
        /// Forces saving in case of a concurrency conflict. Syncs RowVersion details with the latest record.
        /// </summary>
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

        /// <summary>
        /// Discards changes and reloads the customer model representation directly from the database.
        /// </summary>
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

        /// <summary>
        /// Invoked when the customer edits are saved successfully. Triggers notification.
        /// </summary>
        protected override void OnSaveSuccess()
        {
            NotifySuccess("Success", $"Customer '{Name}' saved successfully.");
            base.OnSaveSuccess();
        }

        /// <summary>
        /// Triggered when the user cancels or closes editing.
        /// </summary>
        protected override void OnCancel()
        {
            base.OnCancel();
        }

        // Returns printed report header title
        protected override string GetReportTitle() => $"Customer Profile: {Name}";

        // Assembles anonymous schema object formatted for reporting
        protected override object GetReportItem() => new
        {
            Name,
            Email,
            Phone,
            Address,
            ContactsCount = Contacts.Count,
            PrimaryContact = Contacts.FirstOrDefault()?.Name ?? "N/A"
        };

        #endregion
    }
}
