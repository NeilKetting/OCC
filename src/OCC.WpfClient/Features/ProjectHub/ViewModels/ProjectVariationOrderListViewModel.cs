using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.WpfClient.ModelWrappers;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.ProjectHub.ViewModels
{
    public partial class ProjectVariationOrderListViewModel : ListViewModelBase<ProjectVariationOrderWrapper>
    {
        private readonly IProjectVariationOrderService _variationOrderService;
        private readonly IProjectTaskService _projectTaskService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<ProjectVariationOrderListViewModel> _logger;
        private Guid _projectId;
        private List<ProjectVariationOrderWrapper> _allOrders = new();

        public override string ReportTitle => "Project Variation Orders";

        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Date", PropertyName = "Date", Width = 2 },
            new() { Header = "Description", PropertyName = "Description", Width = 4 },
            new() { Header = "Approved By", PropertyName = "ApprovedBy", Width = 2 },
            new() { Header = "Status", PropertyName = "Status", Width = 2 },
            new() { Header = "Invoiced", PropertyName = "IsInvoiced", Width = 1.5 }
        };

        public override IRelayCommand<object>? OpenCommand => OpenOrderCommand;
        public override IRelayCommand<object>? EditCommand => EditOrderCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteOrderCommand;

        public ProjectVariationOrderListViewModel(
            IProjectVariationOrderService variationOrderService,
            IProjectTaskService projectTaskService,
            IDialogService dialogService,
            ILogger<ProjectVariationOrderListViewModel> logger,
            IPdfService pdfService) : base(pdfService)
        {
            _variationOrderService = variationOrderService;
            _projectTaskService = projectTaskService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "Variation Orders";
        }

        public async Task LoadProjectAsync(Guid projectId)
        {
            _projectId = projectId;
            await LoadDataAsync();
        }

        public override async Task LoadDataAsync()
        {
            if (_projectId == Guid.Empty) return;

            IsBusy = true;
            BusyText = "Loading variation orders...";
            try
            {
                var orders = await _variationOrderService.GetVariationOrdersAsync(_projectId);
                _allOrders = orders.Select(o => new ProjectVariationOrderWrapper(o)).ToList();
                FilterItems();
                UpdateOpenCount();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load variation orders");
                NotifyError("Failed to load variation orders", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateOpenCount()
        {
            var openCount = _allOrders.Count(v => !v.IsInvoiced);
            WeakReferenceMessenger.Default.Send(new ProjectVariationCountChangedMessage(_projectId, openCount));
        }

        protected override void FilterItems()
        {
            var filtered = _allOrders.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(o =>
                    (o.Description?.ToLower().Contains(query) ?? false) ||
                    (o.ApprovedBy?.ToLower().Contains(query) ?? false) ||
                    (o.Status?.ToLower().Contains(query) ?? false));
            }

            var list = filtered.ToList();
            Items = new ObservableCollection<ProjectVariationOrderWrapper>(list);
            TotalCount = list.Count;
        }

        [RelayCommand]
        public void AddVariationOrder()
        {
            var order = new ProjectVariationOrder
            {
                ProjectId = _projectId,
                Date = DateTime.Now,
                Status = "Variation Request"
            };
            var wrapper = new ProjectVariationOrderWrapper(order);
            OpenOverlay(new ProjectVariationOrderDetailViewModel(this, wrapper, _variationOrderService, _projectTaskService, _dialogService, _logger, _pdfService));
        }

        [RelayCommand]
        private void OpenOrder(object? parameter)
        {
            _ = EditOrder(parameter);
        }

        [RelayCommand]
        private async Task EditOrder(object? parameter)
        {
            var target = parameter as ProjectVariationOrderWrapper ?? SelectedItem;
            if (target == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading details...";
                var latest = await _variationOrderService.GetVariationOrderAsync(target.Id);
                if (latest != null)
                {
                    target.Model.Description = latest.Description;
                    target.Model.ApprovedBy = latest.ApprovedBy;
                    target.Model.Date = latest.Date;
                    target.Model.AdditionalComments = latest.AdditionalComments;
                    target.Model.Status = latest.Status;
                    target.Model.IsInvoiced = latest.IsInvoiced;
                    target.Model.RowVersion = latest.RowVersion;
                    target.Initialize();

                    OpenOverlay(new ProjectVariationOrderDetailViewModel(this, target, _variationOrderService, _projectTaskService, _dialogService, _logger, _pdfService));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load variation order details");
                NotifyError("Error", "Could not load variation order details. Please try again.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteOrder(object? parameter)
        {
            var target = parameter as ProjectVariationOrderWrapper ?? SelectedItem;
            if (target == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Variation Order",
                "Are you sure you want to delete this variation order? This action cannot be undone.");

            if (!confirmed) return;

            IsBusy = true;
            BusyText = "Deleting variation order...";
            try
            {
                await _variationOrderService.DeleteVariationOrderAsync(target.Id);
                NotifySuccess("Success", "Variation order deleted.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete variation order");
                NotifyError("Failed to delete variation order", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ToggleInvoicedAsync(ProjectVariationOrderWrapper wrapper)
        {
            if (wrapper == null) return;
            try
            {
                wrapper.CommitToModel();
                await _variationOrderService.UpdateVariationOrderAsync(wrapper.Model);
                
                var latest = await _variationOrderService.GetVariationOrderAsync(wrapper.Id);
                if (latest != null)
                {
                    wrapper.Model.RowVersion = latest.RowVersion;
                    wrapper.Initialize();
                }
                
                UpdateOpenCount();
            }
            catch (Exception ex)
            {
                wrapper.IsInvoiced = !wrapper.IsInvoiced; // Revert
                _logger.LogError(ex, "Update failed");
                NotifyError("Update failed", ex.Message);
            }
        }

        public void CloseDetailView() => CloseOverlay();
    }

    public record ProjectVariationCountChangedMessage(Guid ProjectId, int Count);
}
