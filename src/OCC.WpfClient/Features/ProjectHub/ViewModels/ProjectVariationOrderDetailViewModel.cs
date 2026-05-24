using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectVariationOrderDetailViewModel : DetailViewModelBase
    {
        private readonly ProjectVariationOrderListViewModel _parent;
        private readonly ProjectVariationOrderWrapper _wrapper;
        private readonly IProjectVariationOrderService _variationOrderService;
        private readonly IProjectTaskService _projectTaskService;

        public ProjectVariationOrderWrapper Wrapper => _wrapper;

        [ObservableProperty]
        private bool _isTaskCreated;

        public bool CanCreateTask => Wrapper.Status == "Approved" && !IsTaskCreated && Wrapper.Id != Guid.Empty;

        public ProjectVariationOrderDetailViewModel(
            ProjectVariationOrderListViewModel parent,
            ProjectVariationOrderWrapper wrapper,
            IProjectVariationOrderService variationOrderService,
            IProjectTaskService projectTaskService,
            IDialogService dialogService,
            ILogger logger,
            IPdfService pdfService) : base(dialogService, logger, pdfService)
        {
            _parent = parent;
            _wrapper = wrapper;
            _variationOrderService = variationOrderService;
            _projectTaskService = projectTaskService;
            Title = wrapper.Id == Guid.Empty ? "New Variation Order" : "Edit Variation Order";

            _wrapper.PropertyChanged += Wrapper_PropertyChanged;
            _ = CheckIfTaskCreatedAsync();
        }

        private void Wrapper_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectVariationOrderWrapper.Status))
            {
                OnPropertyChanged(nameof(CanCreateTask));
                CreateTaskCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task CheckIfTaskCreatedAsync()
        {
            if (Wrapper.Id == Guid.Empty) return;

            try
            {
                var tasks = await _projectTaskService.GetTasksAsync(Wrapper.ProjectId);
                IsTaskCreated = tasks.Any(t => t.VariationOrderId == Wrapper.Id);
                OnPropertyChanged(nameof(CanCreateTask));
                CreateTaskCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if task is created for variation order {Id}", Wrapper.Id);
            }
        }

        [RelayCommand(CanExecute = nameof(CanCreateTask))]
        private void CreateTask()
        {
            Wrapper.CommitToModel();
            WeakReferenceMessenger.Default.Send(new CreateTaskFromVariationOrderMessage(Wrapper.Model));
            Wrapper.PropertyChanged -= Wrapper_PropertyChanged;
            _parent.CloseDetailView();
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
            Wrapper.PropertyChanged -= Wrapper_PropertyChanged;
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
            Wrapper.PropertyChanged -= Wrapper_PropertyChanged;
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
