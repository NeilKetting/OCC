using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    public partial class EmailOptionItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private bool _isSelected = true;
    }

    public partial class SelectEmailViewModel : ViewModelBase
    {
        [ObservableProperty] private string _supplierName = string.Empty;
        [ObservableProperty] private string _customEmail = string.Empty;
        [ObservableProperty] private ObservableCollection<EmailOptionItemViewModel> _emailOptions = new();

        public event Action<string?>? Completed;

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

        [RelayCommand]
        private void Confirm()
        {
            var selectedEmails = EmailOptions
                .Where(o => o.IsSelected && !string.IsNullOrWhiteSpace(o.Email))
                .Select(o => o.Email.Trim())
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

        [RelayCommand]
        private void Cancel()
        {
            Completed?.Invoke(null);
        }
    }
}
