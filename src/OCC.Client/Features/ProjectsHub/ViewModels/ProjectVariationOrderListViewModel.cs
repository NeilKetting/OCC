using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ModelWrappers;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.ProjectsHub.ViewModels
{
    public partial class ProjectVariationOrderListViewModel : ViewModelBase
    {
        private readonly IProjectVariationOrderService _variationOrderService;
        private readonly IToastService _toastService;
        private Guid _projectId;

        [ObservableProperty]
        private ObservableCollection<ProjectVariationOrderWrapper> _variationOrders = new();

        [ObservableProperty]
        private string _activeSubTab = "List"; // List or Create

        [ObservableProperty]
        private ProjectVariationOrderWrapper? _newOrder;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isLoading;

        public ProjectVariationOrderListViewModel(
            IProjectVariationOrderService variationOrderService,
            IToastService toastService)
        {
            _variationOrderService = variationOrderService;
            _toastService = toastService;
        }

        public ProjectVariationOrderListViewModel()
        {
            _variationOrderService = null!;
            _toastService = null!;
        }

        public async void LoadProject(Guid projectId)
        {
            _projectId = projectId;
            ActiveSubTab = "List";
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (_projectId == Guid.Empty) return;

            IsLoading = true;
            try
            {
                var orders = await _variationOrderService.GetVariationOrdersAsync(_projectId);
                VariationOrders = new ObservableCollection<ProjectVariationOrderWrapper>(
                    orders.Select(o => new ProjectVariationOrderWrapper(o))
                );
                
                UpdateOpenCount();
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Failed to load variation orders", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateOpenCount()
        {
            var openCount = VariationOrders.Count(v => !v.IsInvoiced);
            WeakReferenceMessenger.Default.Send(new ProjectVariationCountChangedMessage(_projectId, openCount));
        }

        [RelayCommand]
        private void PrepareCreate()
        {
            IsEditing = false;
            NewOrder = new ProjectVariationOrderWrapper(new ProjectVariationOrder 
            { 
                ProjectId = _projectId,
                Date = DateTime.Now,
                Status = "Variation Request"
            });
            ActiveSubTab = "Create";
        }

        [RelayCommand]
        private void PrepareEdit(ProjectVariationOrderWrapper wrapper)
        {
            IsEditing = true;
            // Create a copy for editing
            var modelCopy = new ProjectVariationOrder
            {
                Id = wrapper.Id,
                ProjectId = wrapper.ProjectId,
                Description = wrapper.Description,
                ApprovedBy = wrapper.ApprovedBy,
                Date = wrapper.Date,
                AdditionalComments = wrapper.AdditionalComments,
                Status = wrapper.Status,
                IsInvoiced = wrapper.IsInvoiced,
                RowVersion = wrapper.RowVersion
            };
            NewOrder = new ProjectVariationOrderWrapper(modelCopy);
            ActiveSubTab = "Create";
        }

        [RelayCommand]
        private void CancelCreate()
        {
            NewOrder = null;
            IsEditing = false;
            ActiveSubTab = "List";
        }

        [RelayCommand]
        private async Task SaveOrderAsync()
        {
            if (NewOrder == null) return;

            NewOrder.Validate();
            if (NewOrder.HasErrors)
            {
                _toastService.ShowWarning("Validation Error", "Please fix errors before saving.");
                return;
            }

            IsLoading = true;
            try
            {
                NewOrder.CommitToModel();
                if (IsEditing)
                {
                    await _variationOrderService.UpdateVariationOrderAsync(NewOrder.Model);
                    _toastService.ShowSuccess("Success", "Variation order updated.");
                }
                else
                {
                    await _variationOrderService.CreateVariationOrderAsync(NewOrder.Model);
                    _toastService.ShowSuccess("Success", "Variation order created.");
                }
                await RefreshAsync();
                NewOrder = null;
                IsEditing = false;
                ActiveSubTab = "List";
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Failed to {(IsEditing ? "update" : "create")} variation order", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteVariationAsync(ProjectVariationOrderWrapper wrapper)
        {
            if (wrapper == null) return;

            IsLoading = true;
            try
            {
                await _variationOrderService.DeleteVariationOrderAsync(wrapper.Id);
                _toastService.ShowSuccess("Success", "Variation order deleted.");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Failed to delete variation order", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ToggleInvoicedAsync(ProjectVariationOrderWrapper wrapper)
        {
            try
            {
                wrapper.IsInvoiced = !wrapper.IsInvoiced;
                wrapper.CommitToModel();
                await _variationOrderService.UpdateVariationOrderAsync(wrapper.Model);
                UpdateOpenCount();
            }
            catch (Exception ex)
            {
                wrapper.IsInvoiced = !wrapper.IsInvoiced; // Revert
                _toastService.ShowError("Update failed", ex.Message);
            }
        }
    }

    public record ProjectVariationCountChangedMessage(Guid ProjectId, int Count);
}




