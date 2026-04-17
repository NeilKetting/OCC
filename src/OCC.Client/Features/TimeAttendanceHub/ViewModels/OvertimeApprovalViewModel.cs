using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class OvertimeApprovalViewModel : ViewModelBase, IRecipient<EntityChangedMessage<OvertimeRequest>>
    {
        private readonly IRepository<OvertimeRequest> _overtimeRepository;
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;

        [ObservableProperty]
        private ObservableCollection<OvertimeRequest> _pendingRequests = new();

        [ObservableProperty]
        private OvertimeRequest? _selectedRequest;

        [ObservableProperty]
        private string _rejectionReason = string.Empty;



        public OvertimeApprovalViewModel(
            IRepository<OvertimeRequest> overtimeRepository,
            IAuthService authService,
            INotificationService notificationService)
        {
            _overtimeRepository = overtimeRepository;
            _authService = authService;
            _notificationService = notificationService;
            
            WeakReferenceMessenger.Default.Register(this);

            LoadDataCommand.Execute(null);
        }

        public void Receive(EntityChangedMessage<OvertimeRequest> message)
        {
            if (message.ChangeType == EntityChangeType.Created)
            {
                _ = LoadDataAsync();
            }
        }

        public OvertimeApprovalViewModel()
        {
             _overtimeRepository = null!;
             _authService = null!;
             _notificationService = null!;
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var requests = await _overtimeRepository.GetAllAsync();
                var pending = requests.Where(r => r.Status == LeaveStatus.Pending).OrderBy(r => r.Date);
                
                PendingRequests.Clear();
                foreach (var r in pending) PendingRequests.Add(r);
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", $"Failed to load overtime requests: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ApproveAsync(OvertimeRequest? request)
        {
            if (request == null) return;
            
            IsBusy = true;
            try
            {
                request.Status = LeaveStatus.Approved;
                // request.ApproverId = _authService.CurrentUser?.Id; // If we had ApproverId
                
                await _overtimeRepository.UpdateAsync(request);
                PendingRequests.Remove(request);
                await _notificationService.SendReminderAsync("Success", "Overtime Request Approved.");
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", "Error approving: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RejectAsync(OvertimeRequest? request)
        {
             if (request == null) return;
             
             if (string.IsNullOrWhiteSpace(RejectionReason))
             {
                 await _notificationService.SendReminderAsync("Error", "Rejection reason is required.");
                 return;
             }

            IsBusy = true;
            try
            {
                request.Status = LeaveStatus.Rejected;
                request.RejectionReason = RejectionReason;
                // request.ApproverId = _authService.CurrentUser?.Id;

                await _overtimeRepository.UpdateAsync(request);
                PendingRequests.Remove(request);
                RejectionReason = string.Empty;
                await _notificationService.SendReminderAsync("Success", "Overtime Request Rejected.");
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", "Error: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
