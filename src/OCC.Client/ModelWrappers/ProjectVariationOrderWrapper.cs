using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace OCC.Client.ModelWrappers
{
    public partial class ProjectVariationOrderWrapper : ObservableValidator
    {
        private readonly ProjectVariationOrder _model;

        public ProjectVariationOrderWrapper(ProjectVariationOrder model)
        {
            _model = model;
            Initialize();
        }

        public ProjectVariationOrder Model => _model;

        public Guid Id => _model.Id;
        public Guid ProjectId => _model.ProjectId;

        [ObservableProperty]
        [Required(ErrorMessage = "Description is required")]
        [MinLength(5, ErrorMessage = "Description must be at least 5 characters")]
        private string _description = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Approved By is required")]
        private string _approvedBy = string.Empty;

        [ObservableProperty]
        private DateTime _date = DateTime.Now;

        [ObservableProperty]
        private string _additionalComments = string.Empty;

        [ObservableProperty]
        private string _status = "Variation Request";

        [ObservableProperty]
        private bool _isInvoiced;

        [ObservableProperty]
        private byte[] _rowVersion = Array.Empty<byte>();

        public void Initialize()
        {
            Description = _model.Description;
            ApprovedBy = _model.ApprovedBy;
            Date = _model.Date == default ? DateTime.Now : _model.Date;
            AdditionalComments = _model.AdditionalComments;
            Status = _model.Status;
            IsInvoiced = _model.IsInvoiced;
            RowVersion = _model.RowVersion;
        }

        public void CommitToModel()
        {
            _model.Description = Description;
            _model.ApprovedBy = ApprovedBy;
            _model.Date = Date;
            _model.AdditionalComments = AdditionalComments;
            _model.Status = Status;
            _model.IsInvoiced = IsInvoiced;
            _model.RowVersion = RowVersion;
        }

        public void Validate() => ValidateAllProperties();

        partial void OnDescriptionChanged(string value) => ValidateProperty(value, nameof(Description));
        partial void OnApprovedByChanged(string value) => ValidateProperty(value, nameof(ApprovedBy));
    }
}
