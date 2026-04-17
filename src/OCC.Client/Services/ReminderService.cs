using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;

namespace OCC.Client.Services
{
    public class ReminderService : IReminderService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;
        
        private System.Threading.Timer? _timer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private readonly HashSet<Guid> _notifiedTaskIds = new();
        
        public event EventHandler<int>? UnreadCountChanged;
        
        public int UnreadCount => _notifiedTaskIds.Count;

        public ReminderService(IServiceProvider serviceProvider, IToastService toastService, IAuthService authService)
        {
            _serviceProvider = serviceProvider;
            _toastService = toastService;
            _authService = authService;

            // Listen for task updates to potentially re-check or clear
            WeakReferenceMessenger.Default.Register<TaskUpdatedMessage>(this, (r, m) => 
            {
               // Optional: Immediate check or debounce
            });
        }

        public void Start()
        {
            if (_timer == null)
            {
                _timer = new System.Threading.Timer(async _ => await CheckRemindersAsync(), null, TimeSpan.Zero, _checkInterval);
            }
        }

        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, 0);
        }

        public async Task CheckRemindersAsync()
        {
            var user = _authService.CurrentUser;
            if (user == null) return;

            try 
            {
                using var scope = _serviceProvider.CreateScope();
                var taskRepo = scope.ServiceProvider.GetRequiredService<IProjectTaskRepository>();
                
                // Fetch tasks with pending reminders
                // Note: We need a way to check efficienty. 
                // For now, load tasks for user and check in memory if API doesn't support "GetPendingReminders"
                
                var tasks = await taskRepo.GetMyTasksAsync(); 
                
                var now = DateTime.Now; // Use local time for reminder comparison as user selects local time
                
                foreach(var task in tasks)
                {
                    if (task.IsReminderSet && task.NextReminderDate.HasValue)
                    {
                        // Check if due and not yet notified (locally)
                        if (task.NextReminderDate.Value <= now && !_notifiedTaskIds.Contains(task.Id))
                        {
                            _notifiedTaskIds.Add(task.Id);
                            Notify(task);
                        }
                    }
                }
                
                UnreadCountChanged?.Invoke(this, UnreadCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking reminders: {ex.Message}");
            }
        }

        private void Notify(ProjectTask task)
        {
            // Show Toast
            _toastService.ShowInfo("Reminder", $"Task Due: {task.Name}");
            // Play Sound (Optional - Toast might handle it or we need generic sound)
        }

        public void MarkAsRead(Guid taskId)
        {
            if (_notifiedTaskIds.Contains(taskId))
            {
                _notifiedTaskIds.Remove(taskId);
                UnreadCountChanged?.Invoke(this, UnreadCount);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
