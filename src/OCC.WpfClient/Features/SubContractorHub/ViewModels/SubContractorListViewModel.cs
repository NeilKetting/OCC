using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Features.SubContractorHub.ViewModels
{
    public partial class SubContractorListViewModel : ListViewModelBase<SubContractorSummaryDto>
    {
        private readonly ISubContractorService _subContractorService;
        private readonly IUserService _userService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SubContractorListViewModel> _logger;
        private readonly LocalSettingsService _settingsService;
        private List<SubContractorSummaryDto> _allContractors = new();

        public override string ReportTitle => "Sub-Contractor Directory";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Name", PropertyName = "Name", Width = 3 },
            new() { Header = "Specialties", PropertyName = "Specialties", Width = 3 },
            new() { Header = "Branch", PropertyName = "Branch", Width = 1.5 },
            new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 },
            new() { Header = "Email", PropertyName = "Email", Width = 2.5 }
        };

        // Column Visibility
        [ObservableProperty] private bool _isBranchVisible = true;
        [ObservableProperty] private bool _isSpecialtiesVisible = true;
        [ObservableProperty] private bool _isPhoneVisible = true;
        [ObservableProperty] private bool _isEmailVisible = true;
        
        [ObservableProperty] private bool _isColumnPickerOpen;

        [ObservableProperty] private string _selectedBranch = "All Branches";
        [ObservableProperty] private string _selectedSpecialty = "All Specialties";
        
        [ObservableProperty] private ObservableCollection<string> _branches = new();
        [ObservableProperty] private ObservableCollection<string> _specialties = new();

        public SubContractorListViewModel(
            ISubContractorService subContractorService,
            IUserService userService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<SubContractorListViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _subContractorService = subContractorService;
            _userService = userService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            Title = "Sub-Contractor Management";
            
            LoadLayout();
            _ = LoadDataAsync();
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.SubContractorListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsBranchVisible = layout.Columns.FirstOrDefault(c => c.Header == "Branch")?.IsVisible ?? true;
                IsSpecialtiesVisible = layout.Columns.FirstOrDefault(c => c.Header == "Specialties")?.IsVisible ?? true;
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? true;
                IsEmailVisible = layout.Columns.FirstOrDefault(c => c.Header == "Email")?.IsVisible ?? true;
            }
        }

        private void SaveLayout()
        {
            var layout = new Features.EmployeeHub.Models.EmployeeListLayout
            {
                Columns = new List<Features.EmployeeHub.Models.ColumnConfig>
                {
                    new() { Header = "Branch", IsVisible = IsBranchVisible },
                    new() { Header = "Specialties", IsVisible = IsSpecialtiesVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible },
                    new() { Header = "Email", IsVisible = IsEmailVisible }
                }
            };
            _settingsService.Settings.SubContractorListLayout = layout;
            _settingsService.Save();
        }

        partial void OnIsBranchVisibleChanged(bool value) => SaveLayout();
        partial void OnIsSpecialtiesVisibleChanged(bool value) => SaveLayout();
        partial void OnIsPhoneVisibleChanged(bool value) => SaveLayout();
        partial void OnIsEmailVisibleChanged(bool value) => SaveLayout();

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading sub-contractors...";
                
                var contractors = await _subContractorService.GetSubContractorSummariesAsync();
                _allContractors = contractors.OrderBy(c => c.Name).ToList();

                // Build lookup lists for filters
                var branchList = new List<string> { "All Branches" };
                branchList.AddRange(_allContractors.Select(c => c.Branch).Where(b => !string.IsNullOrEmpty(b)).Distinct().OrderBy(b => b));
                Branches = new ObservableCollection<string>(branchList);

                var specialtyList = new List<string> { "All Specialties" };
                var allSpecs = _allContractors
                    .Where(c => !string.IsNullOrEmpty(c.Specialties))
                    .SelectMany(c => c.Specialties!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct()
                    .OrderBy(s => s);
                specialtyList.AddRange(allSpecs);
                Specialties = new ObservableCollection<string>(specialtyList);
                
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sub-contractors");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddSubContractor()
        {
            var contractor = new SubContractor();
            OpenOverlay(new SubContractorDetailViewModel(this, contractor, _subContractorService, _userService, _dialogService, _logger, _pdfService));
        }

        [RelayCommand]
        private async Task EditSubContractor(SubContractorSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var contractor = await _subContractorService.GetSubContractorAsync(target.Id);
                if (contractor != null)
                {
                    OpenOverlay(new SubContractorDetailViewModel(this, contractor, _subContractorService, _userService, _dialogService, _logger, _pdfService));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSubContractor(SubContractorSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;
            
            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Sub-Contractor", 
                $"Are you sure you want to delete '{target.Name}'? This action cannot be undone.");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting sub-contractor...";
                var success = await _subContractorService.DeleteSubContractorAsync(target.Id);
                if (success)
                {
                    await LoadDataAsync();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedBranchChanged(string value) => FilterItems();
        partial void OnSelectedSpecialtyChanged(string value) => FilterItems();

        protected override void FilterItems()
        {
            var filtered = _allContractors.AsEnumerable();

            // 1. Search Query
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(c => 
                    (c.Name?.ToLower().Contains(query) ?? false) ||
                    (c.Email?.ToLower().Contains(query) ?? false) ||
                    (c.Specialties?.ToLower().Contains(query) ?? false) ||
                    (c.Phone?.ToLower().Contains(query) ?? false));
            }

            // 2. Branch Filter
            if (SelectedBranch != "All Branches")
            {
                filtered = filtered.Where(c => c.Branch == SelectedBranch);
            }

            // 3. Specialty Filter
            if (SelectedSpecialty != "All Specialties")
            {
                filtered = filtered.Where(c => c.Specialties?.Contains(SelectedSpecialty) ?? false);
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<SubContractorSummaryDto>(result);
            TotalCount = result.Count;
        }

        public void CloseDetailView() => CloseOverlay();
    }
}
