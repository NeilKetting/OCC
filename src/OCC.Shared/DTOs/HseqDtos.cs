using System;
using System.Collections.Generic;
using OCC.Shared.Models;

namespace OCC.Shared.DTOs
{
    public class CustomerSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string LogoUrl { get; set; } = string.Empty;
    }

    public class HseqTrainingSummaryDto
    {
        public Guid Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string TrainingTopic { get; set; } = string.Empty;
        public string CertificateType { get; set; } = string.Empty;
        public DateTime DateCompleted { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Role { get; set; } = string.Empty;
        public string CertificateUrl { get; set; } = string.Empty;
        public string Trainer { get; set; } = string.Empty;
    }

    // ─── Site Deployment DTOs ────────────────────────────────────────────────

    /// <summary> Lightweight representation of a SiteDeployment returned to clients. </summary>
    public class SiteDeploymentDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public double? ProjectLatitude { get; set; }
        public double? ProjectLongitude { get; set; }
        public DateTime DeploymentDate { get; set; }
        public string Label { get; set; } = string.Empty;
        public DeploymentStatus Status { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? ReceivedBySiteManagerName { get; set; }
        public List<SiteDeploymentMemberDto> Members { get; set; } = new();
    }

    /// <summary> Member entry within a SiteDeploymentDto. </summary>
    public class SiteDeploymentMemberDto : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isAbsent;

        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        
        public bool IsAbsent
        {
            get => _isAbsent;
            set
            {
                if (_isAbsent != value)
                {
                    _isAbsent = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsAbsent)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary> Request body for the POST /api/sitedeployments endpoint (create crew). </summary>
    public class CreateSiteDeploymentRequest
    {
        public Guid ProjectId { get; set; }
        public DateTime DeploymentDate { get; set; }
        public string Label { get; set; } = string.Empty;
        /// <summary> List of Employee IDs to allocate to this crew. </summary>
        public List<Guid> MemberEmployeeIds { get; set; } = new();
    }

    /// <summary> Request body for the POST /api/sitedeployments/{id}/receive endpoint. </summary>
    public class ReceiveDeploymentRequest
    {
        public Guid SiteManagerId { get; set; }
        /// <summary> Employee IDs the SM is marking as absent (did not arrive). </summary>
        public List<Guid> AbsentMemberEmployeeIds { get; set; } = new();
        public double? GpsLatitude { get; set; }
        public double? GpsLongitude { get; set; }
    }
}
