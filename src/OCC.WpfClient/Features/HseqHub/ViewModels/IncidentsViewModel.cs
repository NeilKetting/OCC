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
    public partial class IncidentsViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;

        [ObservableProperty]
        private ObservableCollection<IncidentSummaryDto> _incidents = new();

        [ObservableProperty]
        private IncidentSummaryDto? _selectedSummary;

        [ObservableProperty]
        private bool _isTypeVisible = true;

        [ObservableProperty]
        private bool _isSeverityVisible = true;

        [ObservableProperty]
        private bool _isStatusVisible = true;

        public IncidentEditorViewModel Editor { get; }

        public IncidentsViewModel(IHealthSafetyService hseqService, IncidentEditorViewModel editor)
        {
            _hseqService = hseqService;
            Editor = editor;
            Title = "Incidents";
            
            Editor.OnSaved = OnIncidentSaved;

            System.Diagnostics.Debug.WriteLine("IncidentsViewModel: Constructor called. Triggering LoadIncidents.");
            _ = LoadIncidents();
        }

        // Design-time
        public IncidentsViewModel()
        {
            _hseqService = null!;
            Editor = new IncidentEditorViewModel();
        }

        [RelayCommand]
        public async Task LoadIncidents()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading incidents...";
                var data = await _hseqService.GetIncidentsAsync();
                if (data != null)
                {
                    Incidents = new ObservableCollection<IncidentSummaryDto>(data.OrderByDescending(i => i.Date));
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
                SelectedSummary = null;
            }
        }

        [RelayCommand]
        private void CancelAdd()
        {
            Editor.Clear();
            SelectedSummary = null;
        }

        private async Task OnIncidentSaved(IncidentSummaryDto summary)
        {
            var existing = Incidents.FirstOrDefault(i => i.Id == summary.Id);
            if (existing != null)
            {
                var index = Incidents.IndexOf(existing);
                Incidents[index] = summary;
            }
            else
            {
                Incidents.Insert(0, summary);
            }
            
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
                    Incidents.Remove(summary);
                    if (SelectedSummary?.Id == summary.Id) SelectedSummary = null;
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
