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
        
        
        [ObservableProperty] private bool _isSpecialtyPickerOpen;
        [ObservableProperty] private bool _isBranchPickerOpen;

        [ObservableProperty] private string _selectedBranch = "All Branches";
        [ObservableProperty] private string _selectedSpecialty = "All Specialties";
        
        [ObservableProperty] private ObservableCollection<string> _branches = new();
        [ObservableProperty] private ObservableCollection<string> _specialties = new();
        
        // Link standard commands for centralized UI
        public override IRelayCommand<object> OpenCommand => OpenSubContractorCommand;
        public override IRelayCommand<object> EditCommand => EditSubContractorCommand;
        public override IRelayCommand<object> DeleteCommand => DeleteSubContractorCommand;

        private readonly ISignalRService? _signalRService;

        public SubContractorListViewModel(
            ISubContractorService subContractorService,
            IUserService userService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<SubContractorListViewModel> logger,
            IPdfService pdfService,
            ISignalRService? signalRService = null) : base(pdfService)
        {
            _subContractorService = subContractorService;
            _userService = userService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            _signalRService = signalRService;
            Title = "Sub-Contractor Management";

            if (_signalRService != null)
            {
                _signalRService.OnSubContractorChanged += OnSubContractorChangedReceived;
            }
            
            LoadLayout();
            _ = LoadDataAsync();
        }

        private void OnSubContractorChangedReceived(EntityChangeDto<SubContractorSummaryDto> change)
        {
            if (change?.Entity == null) return;
            App.Current?.Dispatcher.Invoke(() =>
            {
                var existing = _allContractors.FirstOrDefault(c => c.Id == change.EntityId || c.Id == change.Entity.Id);
                if (change.Action == "Created" || change.Action == "Create")
                {
                    if (existing == null) _allContractors.Add(change.Entity);
                    else _allContractors[_allContractors.IndexOf(existing)] = change.Entity;
                }
                else if (change.Action == "Updated" || change.Action == "Update")
                {
                    if (existing != null) _allContractors[_allContractors.IndexOf(existing)] = change.Entity;
                    else _allContractors.Add(change.Entity);
                }
                else if (change.Action == "Deleted" || change.Action == "Delete")
                {
                    if (existing != null) _allContractors.Remove(existing);
                }
                FilterItems();
            });
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
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
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
        private void SelectSpecialty(string specialty)
        {
            SelectedSpecialty = specialty;
            IsSpecialtyPickerOpen = false;
        }

        [RelayCommand]
        private void SelectBranch(string branch)
        {
            SelectedBranch = branch;
            IsBranchPickerOpen = false;
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading sub-contractors...";
                
                var contractors = (await _subContractorService.GetSubContractorSummariesAsync()).OrderBy(c => c.Name).ToList();

                // Build lookup lists for filters
                var branchList = new List<string> { "All Branches" };
                branchList.AddRange(contractors.Select(c => c.Branch).Where(b => !string.IsNullOrEmpty(b)).Distinct().OrderBy(b => b!));
                Branches = new ObservableCollection<string>(branchList);

                var specialtyList = new List<string> { "All Specialties" };
                var allSpecs = contractors
                    .Where(c => !string.IsNullOrEmpty(c.Specialties))
                    .SelectMany(c => c.Specialties!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct()
                    .OrderBy(s => s);
                specialtyList.AddRange(allSpecs);
                Specialties = new ObservableCollection<string>(specialtyList);

                if (contractors.Count > 100)
                {
                    // Step 1: Fast render top 100 records
                    _allContractors = contractors.Take(100).ToList();
                    FilterItems();
                    IsBusy = false; // Unblock UI

                    // Step 2: Background hydration of full dataset
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            _allContractors = contractors;
                            FilterItems();
                        });
                    });
                }
                else
                {
                    _allContractors = contractors;
                    FilterItems();
                }
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
            var detailVm = new SubContractorDetailViewModel(contractor, _subContractorService, _userService, _dialogService, _logger, _pdfService);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private async Task OpenSubContractor(object? parameter)
        {
            await EditSubContractor(parameter);
        }

        [RelayCommand]
        private async Task EditSubContractor(object? parameter)
        {
            var target = parameter as SubContractorSummaryDto ?? SelectedItem;
            if (target == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var contractor = await _subContractorService.GetSubContractorAsync(target.Id);
                if (contractor != null)
                {
                    var detailVm = new SubContractorDetailViewModel(contractor, _subContractorService, _userService, _dialogService, _logger, _pdfService);
                    OpenOverlay(detailVm, async (res) =>
                    {
                        if (res is bool saved && saved)
                        {
                            await LoadDataAsync();
                        }
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSubContractor(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            string title = targets.Count > 1 ? "Delete Multiple Sub-Contractors" : "Delete Sub-Contractor";
            string message = targets.Count > 1 
                ? $"You are about to delete {targets.Count} records. This action cannot be undone. Are you sure you want to proceed?"
                : $"Are you sure you want to delete '{targets[0].Name}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = targets.Count > 1 ? "Deleting sub-contractors..." : "Deleting sub-contractor...";
                
                foreach (var target in targets)
                {
                    await _subContractorService.DeleteSubContractorAsync(target.Id);
                }
                
                await LoadDataAsync();
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
                filtered = filtered.Where(c => SearchUtils.MatchesQuery(SearchQuery, c.Name, c.Email, c.Specialties, c.Phone, c.Branch));
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

        
    }
}
