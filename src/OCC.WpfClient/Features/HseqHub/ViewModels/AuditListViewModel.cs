using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Features.HseqHub.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class AuditListViewModel : ListViewModelBase<AuditSummaryDto>
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private bool _isStatusVisible = true;

        [ObservableProperty] private Guid? _projectId;
        [ObservableProperty] private string? _projectName;
        [ObservableProperty] private string? _siteManagerName;

        public void Initialize(Guid projectId, string projectName, string? siteManagerName, bool silent = false)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            SiteManagerName = siteManagerName;
            _ = LoadDataAsync();
        }

        private List<AuditSummaryDto> _allAudits = new();

        public override string ReportTitle => "Health & Safety Compliance Audits";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Date", PropertyName = "Date", Width = 1.2 },
            new() { Header = "Audit #", PropertyName = "AuditNumber", Width = 1.5 },
            new() { Header = "Site", PropertyName = "SiteName", Width = 2.5 },
            new() { Header = "Score", PropertyName = "ActualScore", Width = 1 },
            new() { Header = "Status", PropertyName = "Status", Width = 1.2 }
        };

        private readonly ISignalRService? _signalRService;

        public AuditListViewModel(
            IHealthSafetyService hseqService, 
            IServiceProvider serviceProvider,
            IPdfService pdfService,
            IDialogService dialogService,
            ISignalRService? signalRService = null) : base(pdfService)
        {
            _hseqService = hseqService;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;
            _signalRService = signalRService;
            Title = "Audits";

            if (_signalRService != null)
            {
                _signalRService.OnAuditChanged += OnAuditChangedReceived;
            }

            _ = LoadDataAsync();
        }

        private void OnAuditChangedReceived(EntityChangeDto<AuditSummaryDto> change)
        {
            if (change?.Entity == null) return;
            App.Current?.Dispatcher.Invoke(() =>
            {
                var existing = _allAudits.FirstOrDefault(a => a.Id == change.EntityId || a.Id == change.Entity.Id);
                if (change.Action == "Created" || change.Action == "Create")
                {
                    if (existing == null) _allAudits.Add(change.Entity);
                    else _allAudits[_allAudits.IndexOf(existing)] = change.Entity;
                }
                else if (change.Action == "Updated" || change.Action == "Update")
                {
                    if (existing != null) _allAudits[_allAudits.IndexOf(existing)] = change.Entity;
                    else _allAudits.Add(change.Entity);
                }
                else if (change.Action == "Deleted" || change.Action == "Delete")
                {
                    if (existing != null) _allAudits.Remove(existing);
                }
                FilterItems();
            });
        }

        public override async Task LoadDataAsync()
        {
            if (_hseqService == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading audits...";
                
                var data = await _hseqService.GetAuditsAsync(ProjectId);
                if (data != null)
                {
                    var sorted = data.OrderByDescending(a => a.Date).ToList();

                    if (sorted.Count > 100)
                    {
                        // Step 1: Fast render top 100
                        _allAudits = sorted.Take(100).ToList();
                        FilterItems();
                        IsBusy = false; // Unblock UI

                        // Step 2: Background hydration
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(200);
                            App.Current?.Dispatcher.Invoke(() =>
                            {
                                _allAudits = sorted;
                                FilterItems();
                            });
                        });
                    }
                    else
                    {
                        _allAudits = sorted;
                        FilterItems();
                    }
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Failed to load audits.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }


        protected override void FilterItems()
        {
            IEnumerable<AuditSummaryDto> filtered = _allAudits;

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = filtered.Where(a => SearchUtils.MatchesQuery(SearchQuery, a.AuditNumber, a.SiteName, a.HseqConsultant, a.Status.ToString()));
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<AuditSummaryDto>(result);
            TotalCount = result.Count;
        }

        [RelayCommand]
        private async Task OpenDeviations(AuditSummaryDto summary)
        {
            if (summary == null) return;
            
            var vm = _serviceProvider.GetRequiredService<DeviationDetailViewModel>();
            await vm.Initialize(summary.Id);
            OpenOverlay(vm, (res) => _ = LoadDataAsync());
        }

        [RelayCommand]
        public void CreateNewAudit()
        {
            var vm = _serviceProvider.GetRequiredService<AuditDetailViewModel>();
            vm.InitializeForNew(ProjectId, ProjectName);
            if (!string.IsNullOrEmpty(SiteManagerName))
            {
                vm.CurrentAudit.SiteManager = SiteManagerName;
            }
            OpenOverlay(vm, (res) => _ = LoadDataAsync());
        }

        [RelayCommand]
        public void ImportAuditFromPdf()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                Title = "Select Audit PDF Document to Import"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                try
                {
                    var mappingVm = _serviceProvider.GetRequiredService<AuditPdfMappingViewModel>();
                    mappingVm.Initialize(filePath, ProjectId, ProjectName);
                    
                    OpenOverlay(mappingVm, (res) =>
                    {
                        if (res is HseqAudit mappedAudit)
                        {
                            var detailVm = _serviceProvider.GetRequiredService<AuditDetailViewModel>();
                            detailVm.CurrentAudit = mappedAudit;
                            if (!string.IsNullOrEmpty(SiteManagerName))
                            {
                                detailVm.CurrentAudit.SiteManager = SiteManagerName;
                            }
                            detailVm.IsSiteNameReadOnly = ProjectId.HasValue;
                            detailVm.Title = "Preview Imported Audit";
                            detailVm.ImportedPdfFilePath = filePath;

                            detailVm.Findings.Clear();
                            detailVm.Attachments.Clear();
                            
                            OpenOverlay(detailVm, (detailRes) => _ = LoadDataAsync());
                        }
                    });
                }
                catch (Exception ex)
                {
                    NotifyError("Error", $"Failed to import audit from document: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        public async Task EditAudit(AuditSummaryDto summary)
        {
            if (summary == null) return;
            
            var vm = _serviceProvider.GetRequiredService<AuditDetailViewModel>();
            await vm.InitializeForEdit(summary.Id);
            OpenOverlay(vm, (res) => _ = LoadDataAsync());
        }

        [RelayCommand]
        public async Task DeleteAudit(AuditSummaryDto audit)
        {
            if (audit == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Audit",
                $"Are you sure you want to delete '{audit.AuditNumber}'? This action cannot be undone.");
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting audit...";
                var success = await _hseqService.DeleteAuditAsync(audit.Id);
                if (success)
                {
                    _allAudits.Remove(audit);
                    FilterItems();
                    NotifySuccess("Success", "Audit deleted.");
                }
                else
                {
                    NotifyError("Error", "Failed to delete audit.");
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Exception deleting audit.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
