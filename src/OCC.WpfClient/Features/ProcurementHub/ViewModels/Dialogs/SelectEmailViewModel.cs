using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    /// <summary>
    /// Item view model representing a single email option with selection state.
    /// </summary>
    public partial class EmailOptionItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private bool _isSelected = true;
    }

    /// <summary>
    /// ViewModel for selecting recipient emails when sending purchase orders.
    /// Supports adding new supplier contacts via the AddSupplierContactDialog.
    /// </summary>
    public partial class SelectEmailViewModel : ViewModelBase
    {
        [ObservableProperty] private string _supplierName = string.Empty;
        [ObservableProperty] private string _customEmail = string.Empty;
        [ObservableProperty] private ObservableCollection<EmailOptionItemViewModel> _emailOptions = new();

        /// <summary>
        /// Action callback invoked when user confirms or cancels recipient selection.
        /// </summary>
        public event Action<string?>? Completed;

        /// <summary>
        /// Event raised when the user requests to add a new contact for the supplier.
        /// Passes a callback delegate to receive the newly created <see cref="OCC.Shared.Models.SupplierContact"/>.
        /// </summary>
        public event Action<Action<OCC.Shared.Models.SupplierContact?>>? AddContactRequested;

        /// <summary>
        /// Initializes a new instance of <see cref="SelectEmailViewModel"/>.
        /// </summary>
        /// <param name="supplierName">The supplier company name.</param>
        /// <param name="availableEmails">List of existing email addresses associated with the supplier.</param>
        public SelectEmailViewModel(string supplierName, List<string> availableEmails)
        {
            SupplierName = supplierName;
            
            if (availableEmails != null && availableEmails.Count > 0)
            {
                foreach (var email in availableEmails)
                {
                    EmailOptions.Add(new EmailOptionItemViewModel { Email = email, IsSelected = true });
                }
            }
        }

        /// <summary>
        /// Triggers the request to add a new supplier contact via dialog.
        /// </summary>
        [RelayCommand]
        private void AddContact()
        {
            AddContactRequested?.Invoke(onContactAdded =>
            {
                if (onContactAdded != null && !string.IsNullOrWhiteSpace(onContactAdded.Email))
                {
                    var trimmedEmail = onContactAdded.Email.Trim();
                    var existing = EmailOptions.FirstOrDefault(o => o.Email.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.IsSelected = true;
                    }
                    else
                    {
                        EmailOptions.Add(new EmailOptionItemViewModel { Email = trimmedEmail, IsSelected = true });
                    }
                }
            });
        }

        /// <summary>
        /// Confirms email selection and passes the semicolon-delimited result to the completion callback.
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            var selectedEmails = EmailOptions
                .Where(o => o.IsSelected && !string.IsNullOrWhiteSpace(o.Email))
                .Select(o => o.Email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(CustomEmail))
            {
                var customParsed = EmailHelper.ParseEmailAddresses(CustomEmail);
                foreach (var c in customParsed)
                {
                    if (!selectedEmails.Contains(c, StringComparer.OrdinalIgnoreCase))
                    {
                        selectedEmails.Add(c);
                    }
                }
            }

            var finalResult = string.Join("; ", selectedEmails);
            Completed?.Invoke(finalResult);
        }

        /// <summary>
        /// Cancels email selection.
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Completed?.Invoke(null);
        }
    }
}
