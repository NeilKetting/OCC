using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class IncidentsViewModel : ListViewModelBase<IncidentSummaryDto>
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;

        private List<IncidentSummaryDto> _allIncidents = new();

        public override string ReportTitle => "Health & Safety Incident Log";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Date", PropertyName = "Date", Width = 1.2 },
            new() { Header = "Type", PropertyName = "Type", Width = 1.5 },
            new() { Header = "Location", PropertyName = "Location", Width = 2 },
            new() { Header = "Severity", PropertyName = "Severity", Width = 1 },
            new() { Header = "Status", PropertyName = "Status", Width = 1 }
        };

        // Standard commands for centralized UI
        public override IRelayCommand<object>? OpenCommand => OpenIncidentCommand;
        public override IRelayCommand<object>? EditCommand => EditIncidentCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedIncidentsCommand;

        public IncidentsViewModel(IHealthSafetyService hseqService, IServiceProvider serviceProvider, IPdfService pdfService, IDialogService dialogService) : base(pdfService)
        {
            _hseqService = hseqService;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;
            Title = "Incidents";
            
            _ = LoadDataAsync();
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading incidents...";
                var data = await _hseqService.GetIncidentsAsync();
                if (data != null)
                {
                    _allIncidents = data.OrderByDescending(i => i.Date).ToList();
                    FilterItems();
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Failed to load incidents.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected override void FilterItems()
        {
            IEnumerable<IncidentSummaryDto> filtered = _allIncidents;

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(i =>
                    (i.Location?.ToLower().Contains(query) ?? false) ||
                    (i.Type.ToString().ToLower().Contains(query)) ||
                    (i.Severity.ToString().ToLower().Contains(query)) ||
                    (i.Status.ToString().ToLower().Contains(query)));
            }

            var result = filtered.ToList();
            Items = new ObservableCollection<IncidentSummaryDto>(result);
            TotalCount = result.Count;
        }

        [RelayCommand]
        private void OpenIncident(object? parameter)
        {
            _ = EditIncident(parameter);
        }

        [RelayCommand]
        public async Task EditIncident(object? parameter)
        {
            var summary = parameter as IncidentSummaryDto ?? SelectedItem;
            if (summary == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var detail = await _hseqService.GetIncidentAsync(summary.Id);
                if (detail != null)
                {
                    var vm = _serviceProvider.GetRequiredService<IncidentDetailViewModel>();
                    vm.Initialize(ToEntity(detail), detail.Photos, detail.Documents);
                    OpenOverlay(vm, OnIncidentSaved);
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Failed to load incident details.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void AddIncident()
        {
            var vm = _serviceProvider.GetRequiredService<IncidentDetailViewModel>();
            vm.Initialize(new Incident { Date = DateTime.Now });
            OpenOverlay(vm, OnIncidentSaved);
        }

        private void OnIncidentSaved(object? result)
        {
            if (result is IncidentSummaryDto summary)
            {
                var existing = _allIncidents.FirstOrDefault(i => i.Id == summary.Id);
                if (existing != null)
                {
                    var index = _allIncidents.IndexOf(existing);
                    _allIncidents[index] = summary;
                }
                else
                {
                    _allIncidents.Insert(0, summary);
                }
                
                FilterItems();
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedIncidents(object? parameter)
        {
            List<IncidentSummaryDto> targets = new();
            if (parameter is System.Collections.IList list)
            {
                targets = list.Cast<IncidentSummaryDto>().ToList();
            }
            else if (parameter is IncidentSummaryDto summary)
            {
                targets.Add(summary);
            }
            else if (SelectedItem != null)
            {
                targets.Add(SelectedItem);
            }

            if (!targets.Any()) return;

            string message = targets.Count > 1 
                ? $"Are you sure you want to delete {targets.Count} selected incidents? This action cannot be undone."
                : $"Are you sure you want to delete this incident from '{targets[0].Date:d}'? This action cannot be undone.";

            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Incident", message);
            if (!confirmed) return;
            
            try 
            {
                IsBusy = true;
                BusyText = "Deleting...";
                foreach (var t in targets)
                {
                    await _hseqService.DeleteIncidentAsync(t.Id);
                }
                NotifySuccess("Success", targets.Count > 1 ? "Incidents deleted." : "Incident deleted.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotifyError("Error", "Failed to delete incident(s).");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private Incident ToEntity(IncidentDto dto)
        {
            return new Incident
            {
                Id = dto.Id,
                Date = dto.Date,
                Type = dto.Type,
                Severity = dto.Severity,
                Location = dto.Location,
                Description = dto.Description,
                ReportedByUserId = dto.ReportedByUserId,
                Status = dto.Status,
                InvestigatorId = dto.InvestigatorId,
                RootCause = dto.RootCause,
                CorrectiveAction = dto.CorrectiveAction,
                Photos = dto.Photos?.Select(p => new IncidentPhoto
                {
                    Id = p.Id,
                    IncidentId = dto.Id,
                    FileName = p.FileName,
                    FilePath = p.FilePath,
                    FileSize = p.FileSize,
                    UploadedBy = p.UploadedBy,
                    UploadedAt = p.UploadedAt
                }).ToList() ?? new List<IncidentPhoto>(),
                Documents = dto.Documents?.Select(d => new IncidentDocument
                {
                    Id = d.Id,
                    IncidentId = dto.Id,
                    FileName = d.FileName,
                    FilePath = d.FilePath,
                    FileSize = d.FileSize,
                    UploadedBy = d.UploadedBy,
                    UploadedAt = d.UploadedAt
                }).ToList() ?? new List<IncidentDocument>()
            };
        }
    }
}
