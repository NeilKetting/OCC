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

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class SubContractorListViewModel : ListViewModelBase<SubContractorSummaryDto>
    {
        private readonly ISubContractorService _subContractorService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SubContractorListViewModel> _logger;
        private List<SubContractorSummaryDto> _allContractors = new();

        [ObservableProperty] private string _selectedBranch = "All Branches";
        [ObservableProperty] private string _selectedSpecialty = "All Specialties";
        
        [ObservableProperty] private ObservableCollection<string> _branches = new();
        [ObservableProperty] private ObservableCollection<string> _specialties = new();

        public SubContractorListViewModel(
            ISubContractorService subContractorService,
            IDialogService dialogService,
            ILogger<SubContractorListViewModel> logger)
        {
            _subContractorService = subContractorService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "Sub-Contractor Management";
            
            _ = LoadDataAsync();
        }

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
            OpenOverlay(new SubContractorDetailViewModel(this, contractor, _subContractorService, _dialogService, _logger));
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
                    OpenOverlay(new SubContractorDetailViewModel(this, contractor, _subContractorService, _dialogService, _logger));
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
