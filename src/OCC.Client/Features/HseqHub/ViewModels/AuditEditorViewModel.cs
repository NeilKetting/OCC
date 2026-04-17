using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Interfaces;
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
    public partial class AuditEditorViewModel : ViewModelBase
    {
        private readonly IHealthSafetyService _hseqService;
        private readonly IToastService _toastService;

        public event EventHandler? RequestClose;
        public event EventHandler? AuditSaved;

        [ObservableProperty]
        private HseqAudit _currentAudit = new();

        [ObservableProperty]
        private string _title = "New Audit";

        [ObservableProperty]
        private ObservableCollection<HseqAuditNonComplianceItemWrapper> _findings = new();

        [ObservableProperty]
        private ObservableCollection<AuditAttachmentDto> _attachments = new();

        public AuditEditorViewModel(IHealthSafetyService hseqService, IToastService toastService)
        {
            _hseqService = hseqService;
            _toastService = toastService;
        }

        // Design-time constructor
        public AuditEditorViewModel()
        {
            _hseqService = null!;
            _toastService = null!;
        }

        public void InitializeForNew()
        {
            Title = "New Audit";
            Findings.Clear();
            Attachments.Clear();

            var newAudit = new HseqAudit
            {
                Id = Guid.Empty, // Key Fix: ensure it's empty so Save() knows it's new
                Date = DateTime.Today,
                Status = OCC.Shared.Enums.AuditStatus.InProgress,
                TargetScore = 100
            };
            
            // Seed default sections
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
                     _toastService.ShowError("Error", "Audit not found.");
                     RequestClose?.Invoke(this, EventArgs.Empty);
                     return;
                }

                var loadedAudit = ToEntity(auditDto);

                // Ensure sections exist (legacy support)
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
                
                // Ensure "Facilities" exists for older audits being edited
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
                _toastService.ShowError("Error", "Failed to load audit details.");
                System.Diagnostics.Debug.WriteLine(ex);
                RequestClose?.Invoke(this, EventArgs.Empty);
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
        public virtual async Task Save()
        {
            // Commit all findings
            foreach(var f in Findings) f.CommitToModel();

            // Clamp section scores and calculate Total Score
            if (CurrentAudit.Sections != null && CurrentAudit.Sections.Any())
            {
                decimal totalActual = 0;
                decimal totalPossible = 0;

                foreach (var section in CurrentAudit.Sections)
                {
                    // Ensure scores are within valid range (0-100)
                    section.ActualScore = Math.Max(0, Math.Min(section.PossibleScore, section.ActualScore));
                    
                    totalActual += section.ActualScore;
                    totalPossible += section.PossibleScore;
                }
                
                if (totalPossible > 0)
                {
                    // Cap final score at 100%
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
                // Key Logic: If ID is Empty, it's a new audit.
                if (CurrentAudit.Id == Guid.Empty)
                {
                     await CreateInternal();
                }
                else
                {
                     // Try Update.
                     bool success = await _hseqService.UpdateAuditAsync(ToDto(CurrentAudit));
                     if (success)
                     {
                         _toastService.ShowSuccess("Saved", "Audit updated.");
                         await InitializeForEdit(CurrentAudit.Id); // Reload to get latest RowVersion
                         AuditSaved?.Invoke(this, EventArgs.Empty);
                     }
                     else
                     {
                         _toastService.ShowError("Error", "Failed to update audit. This might be a concurrency conflict; please refresh and try again.");
                     }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", $"Failed to save: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateInternal()
        {
             var createdDto = await _hseqService.CreateAuditAsync(ToDto(CurrentAudit));
             if (createdDto != null)
             {
                  CurrentAudit.Id = createdDto.Id;
                  CurrentAudit.RowVersion = createdDto.RowVersion ?? Array.Empty<byte>();
                  
                  // Map generated section IDs back to our UI models so subsequent saves work
                  if (CurrentAudit.Sections != null && createdDto.Sections != null)
                  {
                      foreach (var section in CurrentAudit.Sections)
                      {
                          var matchedDbSection = createdDto.Sections.FirstOrDefault(s => s.Name == section.Name);
                          if (matchedDbSection != null && matchedDbSection.Id != Guid.Empty)
                          {
                              section.Id = matchedDbSection.Id;
                              section.RowVersion = matchedDbSection.RowVersion ?? Array.Empty<byte>();
                          }
                      }
                  }

                  // Sync findings (NonComplianceItems)
                  if (CurrentAudit.NonComplianceItems != null && createdDto.NonComplianceItems != null)
                  {
                      foreach (var item in CurrentAudit.NonComplianceItems)
                      {
                          var matchedDbItem = createdDto.NonComplianceItems.FirstOrDefault(i => 
                              (i.Id != Guid.Empty && i.Id == item.Id) || 
                              (i.Description == item.Description && i.TargetDate == item.TargetDate));

                          if (matchedDbItem != null)
                          {
                              item.Id = matchedDbItem.Id;
                              item.RowVersion = matchedDbItem.RowVersion ?? Array.Empty<byte>();
                          }
                      }
                  }

                  _toastService.ShowSuccess("Created", "New audit created.");
                  AuditSaved?.Invoke(this, EventArgs.Empty);
                  RequestClose?.Invoke(this, EventArgs.Empty);
             }
             else
             {
                  _toastService.ShowError("Error", "Failed to create audit.");
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
        public async Task UploadFiles(object param)
        {
            if (param == null) return;

            HseqAuditNonComplianceItem? targetFinding = null;
            IEnumerable<IStorageFile>? storageFiles = null;

            if (param is Tuple<object, HseqAuditNonComplianceItem> tuple)
            {
                var filesPart = tuple.Item1;
                targetFinding = tuple.Item2;
                
                if (filesPart is IEnumerable<IStorageFile> f) storageFiles = f;
                else if (filesPart is IEnumerable<IStorageItem> i) storageFiles = i.OfType<IStorageFile>();
            }
            else if (param is IEnumerable<IStorageFile> sFiles) storageFiles = sFiles;
            else if (param is IEnumerable<IStorageItem> sItems) storageFiles = sItems.OfType<IStorageFile>();
            else if (param is IStorageFile sFile) storageFiles = new[] { sFile };

            if (storageFiles == null || !storageFiles.Any()) return;

            IsBusy = true;
            try
            {
                // Must ensure Audit exists
                if (CurrentAudit.Id == Guid.Empty) // Strict check for "Is Saved"
                {
                     await CreateInternal();
                     if (CurrentAudit.Id == Guid.Empty) return; // failed
                }

                int count = 0;
                foreach (var file in storageFiles)
                {
                    BusyText = $"Uploading {file.Name}...";
                    using var stream = await file.OpenReadAsync();
                    var metadata = new HseqAuditAttachment
                    {
                        AuditId = CurrentAudit.Id,
                        NonComplianceItemId = targetFinding?.Id,
                        FileName = file.Name,
                        UploadedBy = "CurrentUser"
                    };

                    var result = await _hseqService.UploadAuditAttachmentAsync(metadata, stream, file.Name);
                    
                    if (result != null)
                    {
                        count++;
                        
                        if (targetFinding != null)
                        {
                            // Add to finding wrapper if exists
                            var wrapper = Findings.FirstOrDefault(f => f.Model.Id == targetFinding.Id);
                            if (wrapper != null) wrapper.Attachments.Add(result);
                            
                            // Add to finding model
                            if (targetFinding.Attachments == null) targetFinding.Attachments = new List<HseqAuditAttachment>();
                            targetFinding.Attachments.Add(ToAttachmentEntity(result));
                        }
                        else
                        {
                            Attachments.Add(result);
                            if (CurrentAudit.Attachments == null) CurrentAudit.Attachments = new List<HseqAuditAttachment>();
                            CurrentAudit.Attachments.Add(ToAttachmentEntity(result));
                        }
                    }
                }
                
                if (count > 0) _toastService.ShowSuccess("Success", $"Uploaded {count} file(s).");
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Error", "Failed to upload file(s).");
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public virtual async Task DeleteAttachment(AuditAttachmentDto attachment)
        {
            if (attachment == null) return;
            var result = await _hseqService.DeleteAuditAttachmentAsync(attachment.Id);
            if (result)
            {
                Attachments.Remove(attachment);
                var entity = CurrentAudit.Attachments.FirstOrDefault(a => a.Id == attachment.Id);
                if (entity != null) CurrentAudit.Attachments.Remove(entity);
                _toastService.ShowSuccess("Deleted", "Attachment removed.");
            }
            else
            {
                _toastService.ShowError("Error", "Failed to delete attachment.");
            }
        }

        [RelayCommand]
        public void DeleteFinding(HseqAuditNonComplianceItem item)
        {
            if (item == null) return;
            
            if (CurrentAudit.NonComplianceItems != null)
            {
                CurrentAudit.NonComplianceItems.Remove(item);
            }
            
            var wrapper = Findings.FirstOrDefault(w => w.Model.Id == item.Id);
            if (wrapper != null) Findings.Remove(wrapper);
        }

        #region Mappers
        // Copied from Monolith and adapted

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
