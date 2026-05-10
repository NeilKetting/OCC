using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class AuditsViewModel : ListViewModelBase<AuditSummaryDto>
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private bool _isStatusVisible = true;

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

        public AuditsViewModel(
            IHealthSafetyService hseqService, 
            IServiceProvider serviceProvider,
            IPdfService pdfService) : base(pdfService)
        {
            _hseqService = hseqService;
            _serviceProvider = serviceProvider;
            Title = "Audits";

            _ = LoadDataAsync();
        }

        public override async Task LoadDataAsync()
        {
            if (_hseqService == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading audits...";
                
                var data = await _hseqService.GetAuditsAsync();
                if (data != null)
                {
                    _allAudits = data.OrderByDescending(a => a.Date).ToList();
                    FilterItems();
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
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(a =>
                    (a.AuditNumber?.ToLower().Contains(query) ?? false) ||
                    (a.SiteName?.ToLower().Contains(query) ?? false) ||
                    (a.HseqConsultant?.ToLower().Contains(query) ?? false) ||
                    (a.Status.ToString().ToLower().Contains(query)));
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
            vm.InitializeForNew();
            OpenOverlay(vm, (res) => _ = LoadDataAsync());
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
