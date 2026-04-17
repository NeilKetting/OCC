using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.HseqHub.ViewModels
{
    public partial class AuditDeviationsViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly IExportService _exportService;

        public event EventHandler? RequestClose;
        public event EventHandler? DeviationsUpdated;

        [ObservableProperty]
        private HseqAudit? _selectedAudit;

        [ObservableProperty]
        private ObservableCollection<HseqAuditNonComplianceItemWrapper> _deviations = new();

        [ObservableProperty]
        private ObservableCollection<Employee> _siteManagers = new();

        public AuditDeviationsViewModel(
            IHealthSafetyService hseqService, 
            IToastService toastService, 
            IRepository<Employee> employeeRepository, 
            IExportService exportService)
        {
            _hseqService = hseqService;
            _toastService = toastService;
            _employeeRepository = employeeRepository;
            _exportService = exportService;
        }

        // Design-time
        public AuditDeviationsViewModel()
        {
            _hseqService = null!;
            _toastService = null!;
            _employeeRepository = null!;
            _exportService = null!;
        }

        public async Task Initialize(Guid auditId)
        {
            IsBusy = true;
            try
            {
                // Fetch full audit
                var auditDto = await _hseqService.GetAuditAsync(auditId);
                if (auditDto == null) 
                {
                    _toastService.ShowError("Error", "Audit not found.");
                    RequestClose?.Invoke(this, EventArgs.Empty);
                    return;
                }

                SelectedAudit = ToEntity(auditDto);

                // Load Site Managers if empty
                if (!SiteManagers.Any())
                {
                    var allEmployees = await _employeeRepository.GetAllAsync();
                    if (allEmployees != null)
                    {
                        var managers = allEmployees.Where(e => e.Role == EmployeeRole.SiteManager).ToList();
                        SiteManagers = new ObservableCollection<Employee>(managers);
                    }
                }

                // Populate deviations
                Deviations.Clear();
                if (SelectedAudit.NonComplianceItems != null)
                {
                    foreach (var item in SelectedAudit.NonComplianceItems) 
                    {
                        Deviations.Add(new HseqAuditNonComplianceItemWrapper(item));
                    }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to load deviations.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public void AddDeviation()
        {
            if (SelectedAudit == null) return;
            
            var newItem = new HseqAuditNonComplianceItem
            {
                Id = Guid.NewGuid(),
                AuditId = SelectedAudit.Id,
                Status = OCC.Shared.Enums.AuditItemStatus.Open,
                Description = "", // Initialize strings
                RegulationReference = ""
            };
            
            var wrapper = new HseqAuditNonComplianceItemWrapper(newItem);
            Deviations.Insert(0, wrapper);
            
            // Sync with parent object
            if (SelectedAudit.NonComplianceItems == null) 
                SelectedAudit.NonComplianceItems = new List<HseqAuditNonComplianceItem>();
                
            SelectedAudit.NonComplianceItems.Add(newItem);
        }

        [RelayCommand]
        public async Task SaveDeviation(HseqAuditNonComplianceItemWrapper wrapper)
        {
            if (SelectedAudit == null || wrapper == null) return;
            
            IsBusy = true;
            try
            {
                wrapper.CommitToModel();
                var item = wrapper.Model;

                // Sync
                if (SelectedAudit.NonComplianceItems == null) 
                    SelectedAudit.NonComplianceItems = new List<HseqAuditNonComplianceItem>();

                var existing = SelectedAudit.NonComplianceItems.FirstOrDefault(i => i.Id == item.Id);
                if (existing == null && !SelectedAudit.NonComplianceItems.Contains(item))
                {
                    SelectedAudit.NonComplianceItems.Add(item);
                }
                
                // Save via parent Audit
                var success = await _hseqService.UpdateAuditAsync(ToDto(SelectedAudit));
                if (success)
                {
                    _toastService.ShowSuccess("Saved", "Deviation action saved.");
                    DeviationsUpdated?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _toastService.ShowError("Error", "Failed to save deviation.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to save.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task DeleteDeviation(HseqAuditNonComplianceItem item)
        {
            if (item == null || SelectedAudit == null) return;
            
            var confirm = true; // Could add confirmation dialog here
            if (!confirm) return;

            try
            {
                // Remove from local model
                if (SelectedAudit.NonComplianceItems != null)
                {
                    SelectedAudit.NonComplianceItems.Remove(item);
                }

                var wrapper = Deviations.FirstOrDefault(w => w.Model.Id == item.Id);
                if (wrapper != null) Deviations.Remove(wrapper);

                // Persist change
                IsBusy = true;
                var success = await _hseqService.UpdateAuditAsync(ToDto(SelectedAudit));
                if (success)
                {
                     _toastService.ShowSuccess("Deleted", "Deviation removed.");
                     DeviationsUpdated?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                     _toastService.ShowError("Error", "Failed to update audit.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to delete item.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task GenerateCloseOutReport()
        {
            if (SelectedAudit == null) return;

            IsBusy = true;
            BusyText = "Generating report...";
            try
            {
                // Ensure all items are committed
                foreach (var w in Deviations) w.CommitToModel();

                var filePath = await _exportService.GenerateAuditDeviationReportAsync(SelectedAudit, Deviations.Select(w => w.Model));
                if (!string.IsNullOrEmpty(filePath))
                {
                    await _exportService.OpenFileAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to generate report.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task DeleteAttachment(AuditAttachmentDto attachment)
        {
            if (attachment == null || SelectedAudit == null) return;

            var result = await _hseqService.DeleteAuditAttachmentAsync(attachment.Id);
            if (result)
            {
                // Find in WrappedDeviations
                if (attachment.NonComplianceItemId.HasValue)
                {
                    var deviationWrapper = Deviations.FirstOrDefault(f => f.Model.Id == attachment.NonComplianceItemId.Value);
                    if (deviationWrapper != null) 
                    {
                         var toRemove = deviationWrapper.Attachments.FirstOrDefault(a => a.Id == attachment.Id);
                         if (toRemove != null) deviationWrapper.Attachments.Remove(toRemove);
                    }
                }
                _toastService.ShowSuccess("Deleted", "Attachment removed.");
            }
            else
            {
                _toastService.ShowError("Error", "Failed to delete attachment.");
            }
        }

        #region Mappers
        // Duplicated for now to avoid sharing messy static mappers, or we could extract a Mapper service.
        // For tight coupling in VMs, private mappers are okay.
        
        private HseqAudit ToEntity(AuditDto dto)
        {
             return new HseqAudit
            {
                Id = dto.Id,
                Date = dto.Date,
                SiteName = dto.SiteName,
                ScopeOfWorks = dto.ScopeOfWorks,
                SiteManager = dto.SiteManager,
                SiteSupervisor = dto.SiteSupervisor,
                HseqConsultant = dto.HseqConsultant,
                AuditNumber = dto.AuditNumber,
                TargetScore = dto.TargetScore,
                ActualScore = dto.ActualScore,
                Status = dto.Status,
                CloseOutDate = dto.CloseOutDate,
                RowVersion = dto.RowVersion ?? Array.Empty<byte>(),
                Sections = dto.Sections.Select(s => new HseqAuditSection
                {
                    Id = s.Id,
                    Name = s.Name,
                    PossibleScore = s.PossibleScore,
                    ActualScore = s.ActualScore
                }).ToList(),
                NonComplianceItems = dto.NonComplianceItems.Select(i => new HseqAuditNonComplianceItem
                {
                    Id = i.Id,
                    Description = i.Description,
                    RegulationReference = i.RegulationReference,
                    CorrectiveAction = i.CorrectiveAction,
                    ResponsiblePerson = i.ResponsiblePerson,
                    TargetDate = i.TargetDate,
                    Status = i.Status,
                    ClosedDate = i.ClosedDate,
                    Attachments = i.Attachments.Select(ToAttachmentEntity).ToList()
                }).ToList(),
                Attachments = dto.Attachments.Select(ToAttachmentEntity).ToList()
            };
        }

        private HseqAuditAttachment ToAttachmentEntity(AuditAttachmentDto dto)
        {
            return new HseqAuditAttachment
            {
                Id = dto.Id,
                NonComplianceItemId = dto.NonComplianceItemId,
                FileName = dto.FileName,
                FilePath = dto.FilePath,
                FileSize = dto.FileSize,
                UploadedBy = dto.UploadedBy,
                UploadedAt = dto.UploadedAt
            };
        }

        private AuditAttachmentDto ToAttachmentDto(HseqAuditAttachment entity)
        {
            return new AuditAttachmentDto
            {
                Id = entity.Id,
                NonComplianceItemId = entity.NonComplianceItemId,
                FileName = entity.FileName,
                FilePath = entity.FilePath,
                FileSize = entity.FileSize,
                UploadedBy = entity.UploadedBy,
                UploadedAt = entity.UploadedAt
            };
        }

        private AuditDto ToDto(HseqAudit entity)
        {
            return new AuditDto
            {
                Id = entity.Id,
                Date = entity.Date,
                SiteName = entity.SiteName,
                ScopeOfWorks = entity.ScopeOfWorks,
                SiteManager = entity.SiteManager,
                SiteSupervisor = entity.SiteSupervisor,
                HseqConsultant = entity.HseqConsultant,
                AuditNumber = entity.AuditNumber,
                TargetScore = entity.TargetScore,
                ActualScore = entity.ActualScore,
                Status = entity.Status,
                CloseOutDate = entity.CloseOutDate,
                RowVersion = entity.RowVersion ?? Array.Empty<byte>(),
                Sections = entity.Sections.Select(s => new AuditSectionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    PossibleScore = s.PossibleScore,
                    ActualScore = s.ActualScore
                }).ToList(),
                NonComplianceItems = entity.NonComplianceItems.Select(i => new AuditNonComplianceItemDto
                {
                    Id = i.Id,
                    Description = i.Description,
                    RegulationReference = i.RegulationReference,
                    CorrectiveAction = i.CorrectiveAction,
                    ResponsiblePerson = i.ResponsiblePerson,
                    TargetDate = i.TargetDate,
                    Status = i.Status,
                    ClosedDate = i.ClosedDate,
                    Attachments = i.Attachments.Select(ToAttachmentDto).ToList()
                }).ToList(),
                Attachments = entity.Attachments.Select(ToAttachmentDto).ToList()
            };
        }
        #endregion
    }
}
