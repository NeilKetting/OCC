using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using OCC.Shared.Enums;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

using OCC.Shared.DTOs;

namespace OCC.Client.ModelWrappers
{
    public partial class HseqAuditNonComplianceItemWrapper : ObservableObject
    {
        private readonly HseqAuditNonComplianceItem _model;

        public HseqAuditNonComplianceItemWrapper(HseqAuditNonComplianceItem model)
        {
            _model = model;
            Initialize();
        }

        public HseqAuditNonComplianceItem Model => _model;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _regulationReference = string.Empty;

        [ObservableProperty]
        private string _correctiveAction = string.Empty;

        [ObservableProperty]
        private string _responsiblePerson = string.Empty;

        [ObservableProperty]
        private DateTime? _targetDate;

        [ObservableProperty]
        private DateTime? _closedDate;

        [ObservableProperty]
        private AuditItemStatus _status;

        [ObservableProperty]
        private string _statusColor = "#EF4444"; // Default Red

        [ObservableProperty]
        private ObservableCollection<AuditAttachmentDto> _attachments = new();

        private void Initialize()
        {
            Description = _model.Description;
            RegulationReference = _model.RegulationReference;
            CorrectiveAction = _model.CorrectiveAction;
            ResponsiblePerson = _model.ResponsiblePerson;
            TargetDate = _model.TargetDate;
            ClosedDate = _model.ClosedDate;
            Status = _model.Status;
            
            Attachments = new ObservableCollection<AuditAttachmentDto>(
                (_model.Attachments ?? new List<HseqAuditAttachment>())
                .Select(a => new AuditAttachmentDto
                {
                    Id = a.Id,
                    NonComplianceItemId = a.NonComplianceItemId,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedBy = a.UploadedBy,
                    UploadedAt = a.UploadedAt
                }));
            
            UpdateStatusColor();
        }

        public void CommitToModel()
        {
            _model.Description = Description;
            _model.RegulationReference = RegulationReference;
            _model.CorrectiveAction = CorrectiveAction;
            _model.ResponsiblePerson = ResponsiblePerson;
            _model.TargetDate = TargetDate;
            _model.ClosedDate = ClosedDate;
            _model.Status = Status;
            _model.Attachments = Attachments.Select(d => new HseqAuditAttachment
            {
                Id = d.Id,
                NonComplianceItemId = d.NonComplianceItemId,
                FileName = d.FileName,
                FilePath = d.FilePath,
                FileSize = d.FileSize,
                UploadedBy = d.UploadedBy,
                UploadedAt = d.UploadedAt
            }).ToList();
        }

        partial void OnDescriptionChanged(string value) => _model.Description = value;
        partial void OnRegulationReferenceChanged(string value) => _model.RegulationReference = value;
        partial void OnCorrectiveActionChanged(string value) => _model.CorrectiveAction = value;
        partial void OnResponsiblePersonChanged(string value) => _model.ResponsiblePerson = value;
        
        partial void OnTargetDateChanged(DateTime? value) 
        {
            _model.TargetDate = value;
            UpdateStatusColor();
        }

        partial void OnClosedDateChanged(DateTime? value) => _model.ClosedDate = value;

        partial void OnStatusChanged(AuditItemStatus value)
        {
            _model.Status = value;
            if (value == AuditItemStatus.Closed && ClosedDate == null)
            {
                ClosedDate = DateTime.Now;
            }
            else if (value != AuditItemStatus.Closed)
            {
                ClosedDate = null;
            }
            UpdateStatusColor();
        }

        private void UpdateStatusColor()
        {
            if (Status == AuditItemStatus.Closed)
            {
                StatusColor = "#22C55E"; // Green
                return;
            }

            if (TargetDate == null)
            {
                StatusColor = "#EF4444"; // Red
                return;
            }

            var daysUntil = (TargetDate.Value.Date - DateTime.Today).TotalDays;

            if (daysUntil <= 7)
            {
                StatusColor = "#EAB308"; // Yellow
            }
            else if (daysUntil <= 30)
            {
                StatusColor = "#F97316"; // Orange
            }
            else
            {
                StatusColor = "#F97316"; // Orange (default for > 30 days but not closed)
            }
        }
    }
}
