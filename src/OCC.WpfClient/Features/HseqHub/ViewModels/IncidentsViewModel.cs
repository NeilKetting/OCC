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

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class IncidentsViewModel : ListViewModelBase<IncidentSummaryDto>
    {
        private readonly IHealthSafetyService _hseqService;

        [ObservableProperty]
        private bool _isStatusVisible = true;

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

        public IncidentEditorViewModel Editor { get; }

        public IncidentsViewModel(IHealthSafetyService hseqService, IncidentEditorViewModel editor, IPdfService pdfService) : base(pdfService)
        {
            _hseqService = hseqService;
            Editor = editor;
            Title = "Incidents";
            
            Editor.OnSaved = OnIncidentSaved;

            System.Diagnostics.Debug.WriteLine("IncidentsViewModel: Constructor called. Triggering LoadDataAsync.");
            _ = LoadDataAsync();
        }

        // Design-time
        public IncidentsViewModel() : base(null!)
        {
            _hseqService = null!;
            Editor = new IncidentEditorViewModel();
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
        public async Task OpenIncident(IncidentSummaryDto? summary)
        {
            if (summary == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var detail = await _hseqService.GetIncidentAsync(summary.Id);
                if (detail != null)
                {
                    Editor.Initialize(ToEntity(detail), detail.Photos, detail.Documents);
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
        private void ToggleAdd()
        {
            if (Editor.IsOpen)
            {
                CancelAdd();
            }
            else
            {
                Editor.Initialize(new Incident { Date = DateTime.Now });
                SelectedItem = null;
            }
        }

        [RelayCommand]
        private void CancelAdd()
        {
            Editor.Clear();
            SelectedItem = null;
        }

        private async Task OnIncidentSaved(IncidentSummaryDto summary)
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
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task DeleteIncident(IncidentSummaryDto summary)
        {
            if (summary == null) return;
            
            try 
            {
                IsBusy = true;
                BusyText = "Deleting...";
                var success = await _hseqService.DeleteIncidentAsync(summary.Id);
                if (success)
                {
                    NotifySuccess("Success", "Incident deleted.");
                    _allIncidents.Remove(summary);
                    FilterItems();
                    if (SelectedItem?.Id == summary.Id) SelectedItem = null;
                }
                else
                {
                    NotifyError("Error", "Failed to delete incident.");
                }
            }
            catch (Exception ex)
            {
                 NotifyError("Error", "Exception deleting incident.");
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
