using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectVariationOrderDetailViewModel : DetailViewModelBase
    {
        private readonly ProjectVariationOrderListViewModel _parent;
        private readonly ProjectVariationOrderWrapper _wrapper;
        private readonly IProjectVariationOrderService _variationOrderService;

        public ProjectVariationOrderWrapper Wrapper => _wrapper;

        public ProjectVariationOrderDetailViewModel(
            ProjectVariationOrderListViewModel parent,
            ProjectVariationOrderWrapper wrapper,
            IProjectVariationOrderService variationOrderService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _parent = parent;
            _wrapper = wrapper;
            _variationOrderService = variationOrderService;
            Title = wrapper.Id == Guid.Empty ? "New Variation Order" : "Edit Variation Order";
        }

        protected override async Task ExecuteSaveAsync()
        {
            Wrapper.CommitToModel();
            if (Wrapper.Id == Guid.Empty)
            {
                var created = await _variationOrderService.CreateVariationOrderAsync(Wrapper.Model);
                if (created != null)
                {
                    Wrapper.Model.Id = created.Id;
                    Wrapper.Model.RowVersion = created.RowVersion;
                    Wrapper.Initialize();
                }
            }
            else
            {
                await _variationOrderService.UpdateVariationOrderAsync(Wrapper.Model);
                
                var latest = await _variationOrderService.GetVariationOrderAsync(Wrapper.Id);
                if (latest != null)
                {
                    Wrapper.Model.RowVersion = latest.RowVersion;
                    Wrapper.Initialize();
                }
            }
        }

        protected override async Task<bool> ValidateAsync()
        {
            Wrapper.Validate();
            if (Wrapper.HasErrors)
            {
                ValidationErrors.Clear();
                foreach (var error in Wrapper.GetErrors())
                {
                    ValidationErrors.Add(error.ErrorMessage ?? "Validation error");
                }
                HasErrors = true;
                await PulseValidationAsync();
                return false;
            }
            HasErrors = false;
            return true;
        }

        protected override void OnSaveSuccess()
        {
            NotifySuccess("Success", "Variation order saved successfully.");
            _parent.LoadDataAsync().ConfigureAwait(false);
            _parent.CloseDetailView();
        }

        protected override async Task ExecuteReloadAsync()
        {
            if (Wrapper.Id == Guid.Empty) return;

            try
            {
                var latest = await _variationOrderService.GetVariationOrderAsync(Wrapper.Id);
                if (latest != null)
                {
                    Wrapper.Model.Description = latest.Description;
                    Wrapper.Model.ApprovedBy = latest.ApprovedBy;
                    Wrapper.Model.Date = latest.Date;
                    Wrapper.Model.AdditionalComments = latest.AdditionalComments;
                    Wrapper.Model.Status = latest.Status;
                    Wrapper.Model.IsInvoiced = latest.IsInvoiced;
                    Wrapper.Model.RowVersion = latest.RowVersion;

                    Wrapper.Initialize();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload variation order");
                throw;
            }
        }

        protected override async Task<bool> ExecuteForceSaveAsync()
        {
            try
            {
                var latest = await _variationOrderService.GetVariationOrderAsync(Wrapper.Id);
                if (latest != null)
                {
                    Wrapper.Model.RowVersion = latest.RowVersion;
                    Wrapper.Initialize();
                    await _variationOrderService.UpdateVariationOrderAsync(Wrapper.Model);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force save variation order");
                NotifyError("Save Error", $"Failed to force save: {ex.Message}");
                return false;
            }
        }

        protected override void OnCancel()
        {
            _parent.CloseDetailView();
        }

        protected override string GetReportTitle() => $"Variation Order: {Wrapper.Description}";

        protected override object GetReportItem() => new
        {
            Description = Wrapper.Description,
            ApprovedBy = Wrapper.ApprovedBy,
            Date = Wrapper.Date.ToString("dd MMM yyyy"),
            Status = Wrapper.Status,
            IsInvoiced = Wrapper.IsInvoiced ? "Yes" : "No",
            AdditionalComments = Wrapper.AdditionalComments
        };
    }
}
