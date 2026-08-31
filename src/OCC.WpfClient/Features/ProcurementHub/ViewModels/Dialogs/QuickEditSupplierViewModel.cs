using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using System;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    /// <summary>
    /// Result payload returned when supplier details are updated via the quick edit dialog.
    /// </summary>
    public class QuickEditSupplierResult
    {
        /// <summary>The ID of the supplier being updated.</summary>
        public Guid SupplierId { get; set; }

        /// <summary>The updated primary email address.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>The updated contact person name.</summary>
        public string ContactPerson { get; set; } = string.Empty;

        /// <summary>The updated primary phone number.</summary>
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel for quickly capturing or updating supplier email and contact details without leaving the current view.
    /// </summary>
    public partial class QuickEditSupplierViewModel : ViewModelBase
    {
        [ObservableProperty] private Guid _supplierId;
        [ObservableProperty] private string _supplierName = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _contactPerson = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string? _errorMessage;

        /// <summary>
        /// Action callback invoked when user confirms or cancels the quick edit dialog.
        /// </summary>
        public event Action<QuickEditSupplierResult?>? Completed;

        /// <summary>
        /// Initializes a new instance of <see cref="QuickEditSupplierViewModel"/> with existing supplier info.
        /// </summary>
        /// <param name="supplier">The target supplier to update.</param>
        public QuickEditSupplierViewModel(Supplier supplier)
        {
            if (supplier == null) throw new ArgumentNullException(nameof(supplier));

            SupplierId = supplier.Id;
            SupplierName = supplier.Name;
            Email = supplier.Email ?? string.Empty;
            ContactPerson = supplier.ContactPerson ?? string.Empty;
            Phone = supplier.Phone ?? string.Empty;
            Title = "UPDATE SUPPLIER CONTACT";
        }

        /// <summary>
        /// Validates input and triggers the completion callback with updated details.
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

            var result = new QuickEditSupplierResult
            {
                SupplierId = SupplierId,
                Email = trimmedEmail,
                ContactPerson = ContactPerson?.Trim() ?? string.Empty,
                Phone = Phone?.Trim() ?? string.Empty
            };

            Completed?.Invoke(result);
        }

        /// <summary>
        /// Cancels the quick edit operation.
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Completed?.Invoke(null);
        }
    }
}
