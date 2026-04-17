using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Repositories.Interfaces;
using System.Collections.Generic;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class AuditsViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;

        [ObservableProperty]
        private ObservableCollection<AuditSummaryDto> _audits = new();

        [ObservableProperty]
        private bool _isDeviationsOpen;

        [ObservableProperty]
        private bool _isEditorOpen;

        public AuditEditorViewModel Editor { get; }
        public AuditDeviationsViewModel Deviations { get; }

        public AuditsViewModel(
            IHealthSafetyService hseqService, 
            IToastService toastService,
            AuditEditorViewModel editor,
            AuditDeviationsViewModel deviations)
        {
            _hseqService = hseqService;
            _toastService = toastService;
            Editor = editor;
            Deviations = deviations;

            // Wire up events
            Editor.RequestClose += (s, e) => IsEditorOpen = false;
            Editor.AuditSaved += (s, e) => 
            { 
                IsEditorOpen = false; 
                LoadAuditsCommand.ExecuteAsync(null); 
            };

            Deviations.RequestClose += (s, e) => IsDeviationsOpen = false;
            Deviations.DeviationsUpdated += (s, e) => 
            {
                 // Optional: reload list if status changed?
                 LoadAuditsCommand.ExecuteAsync(null);
            };
        }

        // Design-time constructor
        public AuditsViewModel()
        {
            _hseqService = null!;
            _toastService = null!;
            Editor = new AuditEditorViewModel();
            Deviations = new AuditDeviationsViewModel();
        }

        [RelayCommand]
        public async Task LoadAudits()
        {
            if (_hseqService == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading audits...";
                
                var data = await _hseqService.GetAuditsAsync();
                if (data != null)
                {
                    Audits = new ObservableCollection<AuditSummaryDto>(data.OrderByDescending(a => a.Date));
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Error", "Failed to load audits.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ViewDeviations(AuditSummaryDto summary)
        {
            if (summary == null) return;
            IsEditorOpen = false; // Ensure editor is closed
            await Deviations.Initialize(summary.Id);
            IsDeviationsOpen = true;
        }

        [RelayCommand]
        public void CreateNewAudit()
        {
            IsDeviationsOpen = false;
            Editor.InitializeForNew();
            IsEditorOpen = true;
        }

        [RelayCommand]
        public async Task EditAudit(AuditSummaryDto summary)
        {
            if (summary == null) return;
            IsDeviationsOpen = false;
            await Editor.InitializeForEdit(summary.Id);
            IsEditorOpen = true;
        }

        [RelayCommand]
        public async Task DeleteAudit(AuditSummaryDto audit)
        {
            if (audit == null) return;

            // Confirm?
            // var confirm = await _dialogService.Confirm... (not injected yet, could add)

            try
            {
                IsBusy = true;
                BusyText = "Deleting audit...";
                var success = await _hseqService.DeleteAuditAsync(audit.Id);
                if (success)
                {
                    Audits.Remove(audit);
                    _toastService.ShowSuccess("Success", "Audit deleted.");
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to delete audit.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Exception deleting audit.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void CloseEditor()
        {
            IsEditorOpen = false;
        }

        [RelayCommand]
        private void CloseDeviations()
        {
            IsDeviationsOpen = false;
        }
    }
}
