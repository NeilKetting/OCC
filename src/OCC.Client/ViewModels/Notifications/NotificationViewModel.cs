using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using OCC.Shared.Models;
using OCC.Client.Services;
using System;
using System.Collections.Generic;
using Avalonia.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Infrastructure;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;

using OCC.Client.Infrastructure;

namespace OCC.Client.ViewModels.Notifications
{
    public partial class NotificationViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<Notification> _notifications = new();

        [ObservableProperty]
        private bool _hasNotifications;

        [ObservableProperty]
        private bool _isAdmin;

        [ObservableProperty]
        private int _notificationCount;

        private readonly SignalRNotificationService _signalRService;
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<LeaveRequest> _leaveRepository;
        private readonly IRepository<OvertimeRequest> _overtimeRepository;
        
        private HashSet<Guid> _dismissedEntityIds = new();

        public NotificationViewModel(
            SignalRNotificationService signalRService, 
            IAuthService authService, 
            INotificationService notificationService,
            IRepository<User> userRepository,
            IRepository<LeaveRequest> leaveRepository,
            IRepository<OvertimeRequest> overtimeRepository)
        {
            _signalRService = signalRService;
            _authService = authService;
            _notificationService = notificationService;
            _userRepository = userRepository;
            _leaveRepository = leaveRepository;
            _overtimeRepository = overtimeRepository;
            
            // Check Admin Role
            IsAdmin = _authService.CurrentUser?.UserRole == UserRole.Admin;

            // Initialize with empty list for now
            HasNotifications = Notifications.Count > 0;
            NotificationCount = Notifications.Count;
            Notifications.CollectionChanged += (s, e) => 
            {
                HasNotifications = Notifications.Count > 0;
                NotificationCount = Notifications.Count;
            };

            _signalRService.OnNotificationReceived += OnNotificationReceived;

            InitializeData();
        }

        private async void InitializeData()
        {
            await LoadDismissedItems();

            if (IsAdmin)
            {
                // Load pending approvals initially
                LoadPendingApprovals();
                LoadPendingLeaveRequests();
                LoadPendingOvertimeRequests();
            }
        }
        
        private async Task LoadDismissedItems()
        {
            try
            {
                var ids = await _notificationService.GetDismissedIdsAsync();
                _dismissedEntityIds = new HashSet<Guid>(ids);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading dismissed items: {ex}");
            }
        }

        public NotificationViewModel()
        {
             // Design time
             _signalRService = null!;
             _authService = null!;
             _userRepository = null!;
             _leaveRepository = null!;
             _overtimeRepository = null!;
             _notificationService = null!;
        }

        public void AddSystemNotification(string title, string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Notifications.Insert(0, new Notification
                {
                    Title = title,
                    Message = message,
                    Timestamp = DateTime.Now,
                    IsRead = false,
                    TargetAction = null, // No actions for simple system notifications
                    UserId = null
                });
            });
        }

        // ... methods ...

        private async void LoadPendingApprovals()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                var pending = users.Where(u => !u.IsApproved).ToList();
                
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var user in pending)
                    {
                        // Dedup
                        if (!Notifications.Any(n => n.Message.Contains(user.Email)) && !_dismissedEntityIds.Contains(user.Id))
                        {
                            Notifications.Insert(0, new Notification
                            {
                                Title = "Status: Approval Needed",
                                Message = $"New user awaiting approval: {user.FirstName} {user.LastName} ({user.Email})",
                                Timestamp = DateTime.Now, 
                                IsRead = false,
                                TargetAction = NavigationRoutes.Feature_UserRegistration,
                                UserId = user.Id
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading pending users: {ex}");
            }
        }

        private async void LoadPendingLeaveRequests()
        {
            try
            {
                var requests = await _leaveRepository.GetAllAsync();
                var pending = requests.Where(r => r.Status == LeaveStatus.Pending).ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var req in pending)
                    {
                         // Basic Dedup
                         string msg = $"New Leave Request from {req.Employee?.FirstName} {req.Employee?.LastName}";
                         if (!Notifications.Any(n => n.Message.Contains(req.Id.ToString()) || n.Message == msg) && !_dismissedEntityIds.Contains(req.Id))
                         {
                            Notifications.Insert(0, new Notification
                            {
                                Title = "Status: Leave Request",
                                Message = msg,
                                Timestamp = req.StartDate, 
                                IsRead = false,
                                TargetAction = NavigationRoutes.Feature_LeaveApproval,
                                UserId = req.Id // Storing RequestId
                            });
                         }
                    }
                });
            }
            catch { }
        }

        private async void LoadPendingOvertimeRequests()
        {
            try
            {
                var requests = await _overtimeRepository.GetAllAsync();
                var pending = requests.Where(r => r.Status == LeaveStatus.Pending).ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var req in pending)
                    {
                         string msg = $"New Overtime Request from {req.Employee?.FirstName} {req.Employee?.LastName}";
                         if (!Notifications.Any(n => n.Message == msg) && !_dismissedEntityIds.Contains(req.Id))
                         {
                            Notifications.Insert(0, new Notification
                            {
                                Title = "Status: Overtime Request",
                                Message = msg,
                                Timestamp = req.Date,
                                IsRead = false,
                                TargetAction = NavigationRoutes.Feature_OvertimeRequest,
                                UserId = req.Id // Storing RequestId
                            });
                         }
                    }
                });
            }
            catch { }
        }

        private void OnNotificationReceived(string message)
        {
            // Logic for filtering notifications
            bool isRegistration = message.Contains("New Registration", StringComparison.OrdinalIgnoreCase) || 
                                  message.Contains("registered", StringComparison.OrdinalIgnoreCase) ||
                                  message.Contains("awaiting approval", StringComparison.OrdinalIgnoreCase);

            bool isLeave = message.Contains("Leave Request", StringComparison.OrdinalIgnoreCase);
            bool isOvertime = message.Contains("Overtime Request", StringComparison.OrdinalIgnoreCase);

            bool isAdminNotification = isRegistration || isLeave || isOvertime;

            if (isAdminNotification)
            {
                if (!IsAdmin) return;
            }
            
            // Perform processing/lookup in background then update UI
            Task.Run(async () => 
            {
                Guid? matchedUserId = null;
                string targetAction = string.Empty;
                string title = "Notification";

                if (isRegistration)
                {
                    title = "New Registration";
                    targetAction = NavigationRoutes.Feature_UserRegistration;
                    
                    try 
                    {
                         // Extract Email: "New user awaiting approval: Name (email)"
                         var start = message.LastIndexOf('(');
                         var end = message.LastIndexOf(')');
                         if (start > -1 && end > start)
                         {
                             var email = message.Substring(start + 1, end - start - 1);
                             
                             // Use FindAsync to get user
                             var users = await _userRepository.FindAsync(u => u.Email == email);
                             var user = users.FirstOrDefault();
                             if (user != null)
                             {
                                 matchedUserId = user.Id;
                             }
                         }
                    } 
                    catch { }
                }
                else if (isLeave)
                {
                     title = "Leave Request";
                     targetAction = "ManageLeave"; // Or specific action
                     // Try to match user from name? Simpler to just show message for now.
                     // Message format: "New Leave Request from {Name}"
                }
                else if (isOvertime)
                {
                     title = "Overtime Request";
                     targetAction = "ManageOvertime";
                     title = "Overtime Request";
                     targetAction = "ManageOvertime";
                }
                else if (message.Contains("Update on Bug Report", StringComparison.OrdinalIgnoreCase))
                {
                     // "Update on Bug Report: Description... (For: ReporterName)"
                     // We can try to extract basic info or just set action
                     title = "Bug Report Update";
                     targetAction = NavigationRoutes.Feature_BugReports;
                     // We don't have the ID easily unless we encoded it in message or look it up.
                     // The backend message: "Update on Bug Report: {Desc} (For: {Name})"
                     // The requirement is just to navigate to list view.
                }

                Dispatcher.UIThread.Post(() =>
                {
                    // Avoid duplicates
                    if (!Notifications.Any(n => n.Message == message && (DateTime.Now - n.Timestamp).TotalMinutes < 1))
                    {
                        Notifications.Insert(0, new Notification 
                        { 
                            Title = title, 
                            Message = message, 
                            Timestamp = DateTime.Now,
                            IsRead = false,
                            TargetAction = targetAction, 
                            UserId = matchedUserId 
                        });
                    }
                });
            });
        }

        [RelayCommand]
        private async Task ApproveAction(Notification notification)
        {
            if (!IsAdmin) return;
            
            if (notification.TargetAction == "UserRegistration" && notification.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(notification.UserId.Value);
                if (user != null)
                {
                    user.IsApproved = true;
                    await _userRepository.UpdateAsync(user);
                    
                    Notifications.Remove(notification);
                    WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("User", "Update", user.Id));
                }
            }
            else if (notification.TargetAction == "LeaveRequest" && notification.UserId.HasValue)
            {
                // UserId here holds the Request Id
                var request = await _leaveRepository.GetByIdAsync(notification.UserId.Value);
                if (request != null)
                {
                    request.Status = LeaveStatus.Approved;
                    request.ApproverId = _authService.CurrentUser?.Id;
                    request.ActionedDate = DateTime.Now;
                    await _leaveRepository.UpdateAsync(request);
                    Notifications.Remove(notification);
                    WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("LeaveRequest", "Update", request.Id));
                }
            }
            else if (notification.TargetAction == "OvertimeRequest" && notification.UserId.HasValue)
            {
                var request = await _overtimeRepository.GetByIdAsync(notification.UserId.Value);
                if (request != null)
                {
                    request.Status = LeaveStatus.Approved;
                    request.ApproverId = _authService.CurrentUser?.Id; // Assuming OvertimeRequest has ApproverId? 
                    // Note: OvertimeRequest might likely inherit from or share structure, but checking model might be good. 
                    // Assuming yes for now based on context.
                    await _overtimeRepository.UpdateAsync(request);
                     Notifications.Remove(notification);
                    WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("OvertimeRequest", "Update", request.Id));
                }
            }
            // Fallback for old string-only notifications
            else if (notification.Title.Contains("Approval Needed") || notification.Message.Contains("awaiting approval"))
            {
               await ApproveUserOld(notification);
            }
        }

        [RelayCommand]
        private async Task DenyAction(Notification notification)
        {
            if (!IsAdmin) return;

             if (notification.TargetAction == "UserRegistration" && notification.UserId.HasValue)
            {
                await _userRepository.DeleteAsync(notification.UserId.Value);
                Notifications.Remove(notification);
                 WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("User", "Delete", notification.UserId.Value));
            }
            else if (notification.TargetAction == "LeaveRequest" && notification.UserId.HasValue)
            {
                var request = await _leaveRepository.GetByIdAsync(notification.UserId.Value);
                if (request != null)
                {
                    request.Status = LeaveStatus.Rejected;
                    request.ApproverId = _authService.CurrentUser?.Id;
                    request.ActionedDate = DateTime.Now;
                    await _leaveRepository.UpdateAsync(request);
                    Notifications.Remove(notification);
                    WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("LeaveRequest", "Update", request.Id));
                }
            }
            else if (notification.TargetAction == "OvertimeRequest" && notification.UserId.HasValue)
            {
                var request = await _overtimeRepository.GetByIdAsync(notification.UserId.Value);
                if (request != null)
                {
                    request.Status = LeaveStatus.Rejected;
                    // request.ApproverId ...
                    await _overtimeRepository.UpdateAsync(request);
                    Notifications.Remove(notification);
                    WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("OvertimeRequest", "Update", request.Id));
                }
            }
        }

        [RelayCommand]
        private void Navigate(Notification notification)
        {
             if (notification.TargetAction == NavigationRoutes.Feature_UserRegistration && notification.UserId.HasValue)
             {
                 WeakReferenceMessenger.Default.Send(new OpenManageUsersMessage(notification.UserId.Value));
             }
              else if (notification.TargetAction == NavigationRoutes.Feature_LeaveApproval)
              {
                  WeakReferenceMessenger.Default.Send(new SwitchTabMessage(NavigationRoutes.Feature_LeaveApproval));
              }
              else if (notification.TargetAction == NavigationRoutes.Feature_OvertimeRequest)
              {
                  WeakReferenceMessenger.Default.Send(new SwitchTabMessage(NavigationRoutes.Feature_OvertimeApproval));
              }
              else if (notification.TargetAction == NavigationRoutes.Feature_BugReports)
              {
                  // Go to Bug List
                  WeakReferenceMessenger.Default.Send(new SwitchTabMessage(NavigationRoutes.Feature_BugReports));
                  // Optionally pass ID if we had it, but list is fine for now as requested.
                  WeakReferenceMessenger.Default.Send(new OpenBugReportMessage(null));
              }
        }

        // Kept for fallback
        private async Task ApproveUserOld(Notification notification)
        {
            try 
            {
                var start = notification.Message.LastIndexOf('(');
                var end = notification.Message.LastIndexOf(')');
                
                if (start > -1 && end > start)
                {
                    var email = notification.Message.Substring(start + 1, end - start - 1);
                    var users = await _userRepository.GetAllAsync();
                    var user = users.FirstOrDefault(u => u.Email == email);
                    
                    if (user != null)
                    {
                        user.IsApproved = true;
                        await _userRepository.UpdateAsync(user);
                        
                        Notifications.Remove(notification);
                        WeakReferenceMessenger.Default.Send(new Messages.EntityUpdatedMessage("User", "Update", user.Id));
                    }
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Error approving user from notification: {ex}");
            }
        }
        
        [RelayCommand]
        private async Task ClearNotification(Notification notification)
        {
            if (Notifications.Contains(notification))
            {
                Notifications.Remove(notification);
            }

            // Persist dismissal if it has a target action/entity
            if (notification.UserId.HasValue && !string.IsNullOrEmpty(notification.TargetAction))
            {
                _dismissedEntityIds.Add(notification.UserId.Value);
                await _notificationService.DismissAsync(new NotificationDismissal
                {
                     EntityId = notification.UserId.Value,
                     NotificationType = notification.TargetAction,
                     UserId = _authService.CurrentUser?.Id ?? Guid.Empty // Will be overwritten by controller anyway
                });
            }
        }
    }
}
