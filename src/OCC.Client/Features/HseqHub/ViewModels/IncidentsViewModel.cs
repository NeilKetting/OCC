using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class IncidentsViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;

        [ObservableProperty]
        private ObservableCollection<IncidentSummaryDto> _incidents = new();

        [ObservableProperty]
        private IncidentSummaryDto? _selectedSummary;

        public IncidentEditorViewModel Editor { get; }

        public IncidentsViewModel(IHealthSafetyService hseqService, IToastService toastService, IncidentEditorViewModel editor)
        {
            _hseqService = hseqService;
            _toastService = toastService;
            Editor = editor;
            
            Editor.OnSaved = OnIncidentSaved;

            System.Diagnostics.Debug.WriteLine("IncidentsViewModel: Constructor called. Triggering LoadIncidents.");
            // Trigger load immediately
            _ = LoadIncidents();
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
                _toastService.ShowError("Error", "Failed to load incidents.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedSummaryChanged(IncidentSummaryDto? value)
        {
            // Do nothing on selection change. 
            // User now must double click to open.
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
                _toastService.ShowError("Error", "Failed to load incident details.");
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
            // If it's a new incident (not update), it won't be in our list by ID
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
                    _toastService.ShowSuccess("Success", "Incident deleted.");
                    Incidents.Remove(summary);
                    if (SelectedSummary?.Id == summary.Id) SelectedSummary = null;
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to delete incident.");
                }
            }
            catch (Exception ex)
            {
                 _toastService.ShowError("Error", "Exception deleting incident.");
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
