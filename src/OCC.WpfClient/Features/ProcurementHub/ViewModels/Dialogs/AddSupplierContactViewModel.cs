using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;
using System.Text.RegularExpressions;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel for capturing new contact details for a supplier when emailing orders.
    /// </summary>
    public partial class AddSupplierContactViewModel : ViewModelBase
    {
        [ObservableProperty] private string _supplierName = string.Empty;
        [ObservableProperty] private string _contactName = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _department = "Sales";
        [ObservableProperty] private string? _errorMessage;

        /// <summary>
        /// Action callback invoked when user confirms or cancels the dialog.
        /// </summary>
        public event Action<SupplierContact?>? Completed;

        /// <summary>
        /// Initializes a new instance of <see cref="AddSupplierContactViewModel"/>.
        /// </summary>
        /// <param name="supplierName">The name of the supplier company.</param>
        public AddSupplierContactViewModel(string supplierName)
        {
            SupplierName = supplierName;
            Title = "ADD SUPPLIER CONTACT";
        }

        /// <summary>
        /// Validates input and triggers completed callback with new contact instance.
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Email address is required.";
                return;
            }

            var trimmedEmail = Email.Trim();
            if (!trimmedEmail.Contains('@') || !trimmedEmail.Contains('.'))
            {
                ErrorMessage = "Please enter a valid email address.";
                return;
            }

            var contact = new SupplierContact
            {
                Id = Guid.NewGuid(),
                ContactName = string.IsNullOrWhiteSpace(ContactName) ? SupplierName : ContactName.Trim(),
                Email = trimmedEmail,
                Phone = Phone?.Trim() ?? string.Empty,
                Department = string.IsNullOrWhiteSpace(Department) ? "General" : Department.Trim()
            };

            Completed?.Invoke(contact);
        }

        /// <summary>
        /// Cancels contact addition.
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Completed?.Invoke(null);
        }
    }
}
