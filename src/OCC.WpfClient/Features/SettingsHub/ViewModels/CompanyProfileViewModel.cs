using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.SettingsHub.ViewModels
{
    public partial class CompanyProfileViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<CompanyProfileViewModel> _logger;
        private readonly IPermissionService _permissionService;

        [ObservableProperty]
        private CompanyDetails _companyDetails = new();

        [ObservableProperty]
        private Branch _selectedBranch = Branch.JHB;

        [ObservableProperty]
        private BranchDetails _currentBranchDetails = new();
        
        public List<Branch> AvailableBranches { get; } = Enum.GetValues<Branch>().ToList();

        [ObservableProperty]
        private ObservableCollection<DepartmentEmail> _departments = new();

        public bool IsAdmin { get; }

        public CompanyProfileViewModel(
            ISettingsService settingsService, 
            IDialogService dialogService, 
            ILogger<CompanyProfileViewModel> logger,
            IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _logger = logger;
            _permissionService = permissionService;
            
            Title = "Company Profile";
            IsAdmin = _permissionService.CanAccess("Admin");
            
            LoadData();
        }

        public async void LoadData()
        {
            try
            {
                IsBusy = true;
                CompanyDetails = await _settingsService.GetCompanyDetailsAsync();
                
                // Ensure branches exist
                if (!CompanyDetails.Branches.ContainsKey(Branch.JHB)) CompanyDetails.Branches[Branch.JHB] = new BranchDetails();
                if (!CompanyDetails.Branches.ContainsKey(Branch.CPT)) CompanyDetails.Branches[Branch.CPT] = new BranchDetails();

                RefreshBranchData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
                await _dialogService.ShowAlertAsync("Error", $"Failed to load settings: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedBranchChanged(Branch value)
        {
            RefreshBranchData();
        }

        private void RefreshBranchData()
        {
            if (CompanyDetails.Branches.ContainsKey(SelectedBranch))
            {
                CurrentBranchDetails = CompanyDetails.Branches[SelectedBranch];
                
                Departments.Clear();
                if (CurrentBranchDetails.DepartmentEmails != null)
                {
                    foreach(var d in CurrentBranchDetails.DepartmentEmails) Departments.Add(d);
                }
            }
        }

        [RelayCommand]
        public void AddDepartment()
        {
            Departments.Add(new DepartmentEmail { Department = "New Dept", EmailAddress = "" });
        }

        [RelayCommand]
        public void RemoveDepartment(DepartmentEmail dept)
        {
            if (Departments.Contains(dept))
            {
                Departments.Remove(dept);
            }
        }

        [RelayCommand]
        public async Task Save()
        {
            try
            {
                IsBusy = true;
                
                // Sync back current departments to the current branch details before saving
                if (CompanyDetails.Branches.ContainsKey(SelectedBranch))
                {
                    CompanyDetails.Branches[SelectedBranch].DepartmentEmails = new List<DepartmentEmail>(Departments);
                }

                await _settingsService.SaveCompanyDetailsAsync(CompanyDetails);
                await _dialogService.ShowAlertAsync("Success", "Company details saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                await _dialogService.ShowAlertAsync("Error", $"Failed to save settings: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Close()
        {
            WeakReferenceMessenger.Default.Send(new CloseHubMessage(this));
        }
    }
}
