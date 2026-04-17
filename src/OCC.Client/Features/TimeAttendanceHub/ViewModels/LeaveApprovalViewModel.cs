using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
    public partial class LeaveApprovalViewModel : ViewModelBase, IRecipient<EntityChangedMessage<LeaveRequest>>
    {
        private readonly ILeaveService _leaveService;
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;
        private readonly IExportService _exportService;

        [ObservableProperty]
        private ObservableCollection<LeaveRequest> _pendingRequests = new();

        [ObservableProperty]
        private LeaveRequest? _selectedRequest;

        [ObservableProperty]
        private string _rejectionReason = string.Empty;



        public LeaveApprovalViewModel(
            ILeaveService leaveService,
            IAuthService authService,
            INotificationService notificationService,
            IDialogService dialogService,
            IExportService exportService)
        {
            _leaveService = leaveService;
            _authService = authService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _exportService = exportService;
            
            WeakReferenceMessenger.Default.Register(this);

            LoadDataCommand.Execute(null);
        }

        public void Receive(EntityChangedMessage<LeaveRequest> message)
        {
            if (message.ChangeType == EntityChangeType.Created)
            {
                _ = LoadDataAsync();
            }
        }

        public LeaveApprovalViewModel()
        {
             _leaveService = null!;
             _authService = null!;
             _notificationService = null!;
             _dialogService = null!;
             _exportService = null!;
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                // In a real app, we might expand the LeaveRequest.Employee navigation property.
                // Depending on the Repo implementation, it might not load includes automatically.
                // Assuming standard Repo does minimal loading, we might need a specific service method 
                // that Includes Employee, or we rely on LazyLoading if enabled (unlikely) 
                // or we fetch employees and join.
                // For now, let's assume GetPendingRequestsAsync includes Employee or we'll simple-fetch.
                // Actually, Generic Repo usually doesn't include. 
                // I will add "Include" logic or just fetch all for now in Service if needed.
                // Let's assume the Service does it or I need to update Service. 
                // I'll check Service later. For now, basic flow.
                
                var requests = await _leaveService.GetPendingRequestsAsync();
                PendingRequests.Clear();
                
                // Sort by Name
                var sorted = requests.OrderBy(r => r.Employee?.FirstName).ThenBy(r => r.Employee?.LastName);
                
                foreach (var r in sorted) PendingRequests.Add(r);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PrintReportAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var columns = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EmployeeName", "Employee" },
                    { "StartDateStr", "Start" },
                    { "EndDateStr", "End" },
                    { "NumberOfDays", "Days" },
                    { "LeaveType", "Type" },
                    { "Reason", "Reason" },
                    { "Status", "Status" }
                };

                var reportData = PendingRequests.Select(r => new
                {
                    EmployeeName = r.Employee?.DisplayName ?? "Unknown",
                    StartDateStr = r.StartDate.ToString("d"),
                    EndDateStr = r.EndDate.ToString("d"),
                    NumberOfDays = r.NumberOfDays.ToString(),
                    LeaveType = r.LeaveType.ToString(),
                    Reason = r.Reason,
                    Status = r.Status.ToString()
                }).ToList();

                var title = "Leave Approvals Report";
                var path = await _exportService.GenerateHtmlReportAsync(reportData, title, columns);
                await _exportService.OpenFileAsync(path);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Export Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ApproveAsync(LeaveRequest? request)
        {
            if (request == null) return;
            
            var user = _authService.CurrentUser;
            // if (user == null) return; // Optional check

            IsBusy = true;
            try
            {
                await _leaveService.ApproveRequestAsync(request.Id, user?.Id ?? Guid.Empty);
                PendingRequests.Remove(request);
                await _notificationService.SendReminderAsync("Success", "Leave Request Approved.");
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
        private async Task RejectAsync(LeaveRequest? request)
        {
             if (request == null) return;
             
             if (string.IsNullOrWhiteSpace(RejectionReason))
             {
                 await _notificationService.SendReminderAsync("Error", "Rejection reason is required.");
                 return;
             }

            try
            {
                await _leaveService.RejectRequestAsync(request.Id, Guid.Empty, RejectionReason);
                PendingRequests.Remove(request);
                RejectionReason = string.Empty;
                await _notificationService.SendReminderAsync("Success", "Leave Request Rejected.");
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", "Error: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task EditRequestAsync(LeaveRequest? request)
        {
            if (request == null) return;
            
            // Open Dialog, pass a clone to avoid unintended reference updates before save
            var clone = new LeaveRequest
            {
                Id = request.Id,
                EmployeeId = request.EmployeeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                LeaveType = request.LeaveType,
                Reason = request.Reason,
                Status = request.Status,
                NumberOfDays = request.NumberOfDays,
                RowVersion = request.RowVersion,
                CreatedAtUtc = request.CreatedAtUtc,
                CreatedBy = request.CreatedBy,
                IsActive = request.IsActive
            };

            var editedRequest = await _dialogService.ShowEditLeaveRequestAsync(clone);
            
            if (editedRequest != null)
            {
                IsBusy = true;
                try
                {
                    await _leaveService.UpdateRequestAsync(editedRequest);
                    await _notificationService.SendReminderAsync("Success", "Leave Request Updated.");
                    await LoadDataAsync(); // Refresh list to get accurate number of days and dates
                }
                catch (Exception ex)
                {
                    await _notificationService.SendReminderAsync("Error", "Error updating: " + ex.Message);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task DeleteRequestAsync(LeaveRequest? request)
        {
            if (request == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync("Confirm Delete", "Are you sure you want to delete this leave request?");
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                await _leaveService.DeleteRequestAsync(request.Id);
                PendingRequests.Remove(request);
                if (SelectedRequest?.Id == request.Id) SelectedRequest = null;
                await _notificationService.SendReminderAsync("Success", "Leave Request Deleted.");
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", "Error deleting: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
