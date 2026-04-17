using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using OCC.Client.ViewModels.Core;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class CompanyProfileViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly SideMenuViewModel _sideMenuViewModel;

        [ObservableProperty]
        private CompanyDetails _companyDetails = new();

        [ObservableProperty]
        private Branch _selectedBranch = Branch.JHB;

        [ObservableProperty]
        private BranchDetails _currentBranchDetails = new();
        
        public List<Branch> AvailableBranches { get; } = Enum.GetValues<Branch>().ToList();



        private readonly ILogger<CompanyProfileViewModel> _logger;

        private readonly IPermissionService _permissionService;
        public bool IsAdmin { get; }

        public CompanyProfileViewModel(
            ISettingsService settingsService, 
            IDialogService dialogService, 
            ILogger<CompanyProfileViewModel> logger,
            SideMenuViewModel sideMenuViewModel,
            IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _logger = logger;
            _sideMenuViewModel = sideMenuViewModel;
            _permissionService = permissionService;
            
            IsAdmin = _permissionService.CanAccess("Admin");
            
            LoadData();
        }
        
        public CompanyProfileViewModel() 
        { 
             // Design time
             _settingsService = null!;
             _dialogService = null!;
             _logger = null!;
             _sideMenuViewModel = null!;
             _permissionService = null!;
             CompanyDetails = new CompanyDetails();
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
                if(_dialogService != null) 
                    await _dialogService.ShowAlertAsync("Error", $"Failed to load settings: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<DepartmentEmail> _departments = new();

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
                
                // Sync back
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
            // Navigate back to Home/Dashboard
            _sideMenuViewModel.ActiveSection = OCC.Client.Infrastructure.NavigationRoutes.Home;
        }
    }
}
