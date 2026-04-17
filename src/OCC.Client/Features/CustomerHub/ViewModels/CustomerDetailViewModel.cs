using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace OCC.Client.Features.CustomerHub.ViewModels
{
    public partial class CustomerDetailViewModel : ViewModelBase
    {
        private readonly Services.Interfaces.ICustomerService _customerService;
        private readonly Services.Interfaces.IDialogService _dialogService;
        private Guid? _existingId;

        public event EventHandler? CloseRequested;
        public event EventHandler? Saved;

        public new string Title
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return _existingId.HasValue ? "Edit Customer" : "New Customer";
                }
                return Name;
            }
        }

        [ObservableProperty]
        private string _saveButtonText = "Create Customer";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyPropertyChangedFor(nameof(Title))]
        [Required(ErrorMessage = "Customer name is required.")]
        [MinLength(3, ErrorMessage = "Customer name must be at least 3 characters.")]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _header = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<CustomerContact> _contacts = new();

        [ObservableProperty]
        private bool _isBusy;

        public CustomerDetailViewModel(Services.Interfaces.ICustomerService customerService, Services.Interfaces.IDialogService dialogService)
        {
            _customerService = customerService;
            _dialogService = dialogService;
        }

        public CustomerDetailViewModel()
        {
             _customerService = null!;
             _dialogService = null!;
        }

        public void InitializeNew()
        {
            SaveButtonText = "Create Customer";
            _existingId = null;
        }

        public void Load(Customer customer)
        {
            if (customer == null) return;

            SaveButtonText = "Save Changes";
            _existingId = customer.Id;

            Name = customer.Name;
            Header = customer.Header;
            Email = customer.Email;
            Phone = customer.Phone;
            Address = customer.Address;
            
            Contacts.Clear();
            if (customer.Contacts != null)
            {
                foreach(var c in customer.Contacts) Contacts.Add(c);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            ValidateAllProperties();
            if (HasErrors) return;

            try
            {
                IsBusy = true;

                if (_existingId.HasValue)
                {
                    var existing = await _customerService.GetCustomerAsync(_existingId.Value);
                    if (existing != null)
                    {
                        UpdateModel(existing);
                        await _customerService.UpdateCustomerAsync(existing);
                    }
                }
                else
                {
                    var newCustomer = new Customer();
                    UpdateModel(newCustomer);
                    await _customerService.CreateCustomerAsync(newCustomer);
                }

                Saved?.Invoke(this, EventArgs.Empty);
            }
            catch (OCC.Client.Infrastructure.Exceptions.ConcurrencyException cex)
            {
                await _dialogService.ShowAlertAsync("Concurrency Conflict", cex.Message);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to save customer: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        [RelayCommand]
        public void AddContact()
        {
            Contacts.Add(new CustomerContact { Name = "New Contact", Department = "General" });
        }

        [RelayCommand]
        public void RemoveContact(CustomerContact contact)
        {
            if (Contacts.Contains(contact)) Contacts.Remove(contact);
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && Name.Length >= 3;

        private void UpdateModel(Customer model)
        {
            model.Name = Name;
            model.Header = Header;
            model.Email = Email;
            model.Phone = Phone;
            model.Address = Address;
            model.Contacts = Contacts.ToList();
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
