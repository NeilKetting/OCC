using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace OCC.WpfClient.Features.HseqHub.ViewModels
{
    public partial class AuditDetailViewModel : OverlayViewModel
    {
        private readonly IHealthSafetyService _hseqService;

        [ObservableProperty]
        private HseqAudit _currentAudit = new();

        [ObservableProperty]
        private ObservableCollection<HseqAuditNonComplianceItemWrapper> _findings = new();

        [ObservableProperty]
        private ObservableCollection<AuditAttachmentDto> _attachments = new();

        public AuditDetailViewModel(IHealthSafetyService hseqService)
        {
            _hseqService = hseqService;
            Title = "Audit Details";
        }

        public void InitializeForNew()
        {
            Title = "New Audit";
            Findings.Clear();
            Attachments.Clear();

            var newAudit = new HseqAudit
            {
                Id = Guid.Empty,
                Date = DateTime.Today,
                Status = OCC.Shared.Enums.AuditStatus.InProgress,
                TargetScore = 100
            };
            
            var categories = new[]
            {
                "Administrative Requirements", "Education Training & Promotion", "Public Safety",
                "Personal Protective Equipment (PPE)", "Housekeeping", "Elevated Work", "Electricity",
                "Fire Prevention and Protection", "Equipment", "Construction Vehicles and Mobile Plant",
                "Facilities"
            };

            newAudit.Sections = new List<HseqAuditSection>();
            foreach (var cat in categories)
            {
                newAudit.Sections.Add(new HseqAuditSection 
                { 
                    Name = cat, 
                    PossibleScore = 100, 
                    ActualScore = 0 
                });
            }

            CurrentAudit = newAudit;
        }

        public async Task InitializeForEdit(Guid auditId)
        {
            IsBusy = true;
            try
            {
                var auditDto = await _hseqService.GetAuditAsync(auditId);
                if (auditDto == null) 
                {
                     NotifyError("Error", "Audit not found.");
                     Close();
                     return;
                }

                var loadedAudit = ToEntity(auditDto);

                if (loadedAudit.Sections == null || !loadedAudit.Sections.Any())
                {
                    var categories = new[]
                    {
                        "Administrative Requirements", "Education Training & Promotion", "Public Safety",
                        "Personal Protective Equipment (PPE)", "Housekeeping", "Elevated Work", "Electricity",
                        "Fire Prevention and Protection", "Equipment", "Construction Vehicles and Mobile Plant",
                        "Facilities"
                    };
                    loadedAudit.Sections = new List<HseqAuditSection>();
                    foreach (var cat in categories) 
                    {
                        loadedAudit.Sections.Add(new HseqAuditSection { Name = cat, PossibleScore = 100, ActualScore = 0 });
                    }
                }
                
                if (loadedAudit.Sections != null && !loadedAudit.Sections.Any(s => s.Name == "Facilities"))
                {
                    loadedAudit.Sections.Add(new HseqAuditSection { Name = "Facilities", PossibleScore = 100, ActualScore = 0 });
                }

                CurrentAudit = loadedAudit;
                Attachments = new ObservableCollection<AuditAttachmentDto>(loadedAudit.Attachments.Select(ToAttachmentDto));
                
                Findings.Clear();
                if (loadedAudit.NonComplianceItems != null)
                {
                    foreach (var item in loadedAudit.NonComplianceItems)
                    {
                        Findings.Add(new HseqAuditNonComplianceItemWrapper(item));
                    }
                }

                Title = "Edit Audit Score";
            }
            catch(Exception ex)
            {
                NotifyError("Error", "Failed to load audit details.");
                System.Diagnostics.Debug.WriteLine(ex);
                Close();
            }
            finally 
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task Save()
        {
            foreach(var f in Findings) f.CommitToModel();

            if (CurrentAudit.Sections != null && CurrentAudit.Sections.Any())
            {
                decimal totalActual = 0;
                decimal totalPossible = 0;

                foreach (var section in CurrentAudit.Sections)
                {
                    section.ActualScore = Math.Max(0, Math.Min(section.PossibleScore, section.ActualScore));
                    
                    totalActual += section.ActualScore;
                    totalPossible += section.PossibleScore;
                }
                
                if (totalPossible > 0)
                {
                    CurrentAudit.ActualScore = Math.Min(100m, (totalActual / totalPossible) * 100m);
                    CurrentAudit.ActualScore = Math.Round(CurrentAudit.ActualScore, 2);
                }
                else
                {
                    CurrentAudit.ActualScore = 0;
                }
            }
            
            IsBusy = true;
            try
            {
                if (CurrentAudit.Id == Guid.Empty)
                {
                     var createdDto = await _hseqService.CreateAuditAsync(ToDto(CurrentAudit));
                     if (createdDto != null)
                     {
                          NotifySuccess("Created", "New audit created.");
                          Close(createdDto);
                     }
                     else
                     {
                          NotifyError("Error", "Failed to create audit.");
                     }
                }
                else
                {
                     bool success = await _hseqService.UpdateAuditAsync(ToDto(CurrentAudit));
                     if (success)
                     {
                         NotifySuccess("Saved", "Audit updated.");
                         Close(ToDto(CurrentAudit));
                     }
                     else
                     {
                         NotifyError("Error", "Failed to update audit.");
                     }
                }
            }
            catch (Exception ex)
            {
                NotifyError("Error", $"Failed to save: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void AddFinding()
        {
            var newItem = new HseqAuditNonComplianceItem
            {
                Id = Guid.NewGuid(),
                AuditId = CurrentAudit.Id,
                Status = OCC.Shared.Enums.AuditItemStatus.Open
            };

            if (CurrentAudit.NonComplianceItems == null)
                CurrentAudit.NonComplianceItems = new List<HseqAuditNonComplianceItem>();
            
            CurrentAudit.NonComplianceItems.Add(newItem);
            Findings.Add(new HseqAuditNonComplianceItemWrapper(newItem));
        }

        [RelayCommand]
        public void DeleteFinding(HseqAuditNonComplianceItemWrapper wrapper)
        {
            if (wrapper == null) return;
            
            if (CurrentAudit.NonComplianceItems != null)
            {
                CurrentAudit.NonComplianceItems.Remove(wrapper.Model);
            }
            
            Findings.Remove(wrapper);
        }

        #region Mappers
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
                    ActualScore = s.ActualScore,
                    RowVersion = s.RowVersion ?? Array.Empty<byte>()
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
                    RowVersion = i.RowVersion ?? Array.Empty<byte>(),
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
                    ActualScore = s.ActualScore,
                    RowVersion = s.RowVersion
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
                    RowVersion = i.RowVersion,
                    Attachments = i.Attachments.Select(ToAttachmentDto).ToList()
                }).ToList(),
                Attachments = entity.Attachments.Select(ToAttachmentDto).ToList()
            };
        }
        #endregion
    }
}
