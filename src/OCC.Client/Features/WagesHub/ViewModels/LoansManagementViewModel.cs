using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OCC.Client.Features.WagesHub.ViewModels
{
    public partial class LoansManagementViewModel : ViewModelBase,
        IRecipient<EntityUpdatedMessage>
    {
        private readonly IEmployeeLoanService _loanService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<EmployeeLoan> _loans = new();

        [ObservableProperty]
        private EmployeeLoan? _selectedLoan;

        [ObservableProperty]
        private bool _isLoading;

        public LoansManagementViewModel(IEmployeeLoanService loanService, IDialogService dialogService)
        {
            _loanService = loanService;
            _dialogService = dialogService;

            WeakReferenceMessenger.Default.Register(this);
            LoadLoansCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadLoans()
        {
            try
            {
                IsLoading = true;
                var loans = await _loanService.GetAllAsync();
                Loans = new ObservableCollection<EmployeeLoan>(loans);
            }
            catch (Exception ex)
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Error loading loans: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddLoan()
        {
            var result = await _dialogService.ShowAddLoanAsync();
            if (result != null)
            {
                await _loanService.AddAsync(result);
                await LoadLoans();
            }
        }

        [RelayCommand]
        private async Task TerminateLoan(EmployeeLoan? loan)
        {
            if (loan == null) return;

            // Confirm?
            loan.IsActive = false;
            await _loanService.UpdateAsync(loan);
        }

        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "EmployeeLoan")
            {
                LoadLoansCommand.Execute(null);
            }
        }
    }
}
