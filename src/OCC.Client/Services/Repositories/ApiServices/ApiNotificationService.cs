using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Infrastructure;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiNotificationService : BaseApiService<Notification>, INotificationService
    {
        private readonly SignalRNotificationService _signalRService;

        public event EventHandler<Notification>? NotificationReceived;

        public ApiNotificationService(IAuthService authService, SignalRNotificationService signalRService) : base(authService)
        {
            _signalRService = signalRService;
            
            // Subscribe to real-time events
            _signalRService.OnNotificationReceived += HandleSignalRNotification;
        }

        protected override string ApiEndpoint => "Notifications";

        private void HandleSignalRNotification(string message)
        {
            // Parse message or just trigger a refresh/event
            // For now, let's create a temporary object or just notify
            var notification = new Notification 
            { 
               Title = "New Notification", 
               Message = message, 
               Timestamp = DateTime.Now,
               Type = NotificationType.Alert
            };
            NotificationReceived?.Invoke(this, notification);
        }

        public async Task<IEnumerable<Notification>> GetNotificationsAsync()
        {
            return await GetAllAsync();
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            EnsureAuthorization();
             try 
            {
                await _httpClient.PutAsync($"api/Notifications/{notificationId}/Read", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking notification read: {ex.Message}");
            }
        }

        public async Task<IEnumerable<Guid>> GetDismissedIdsAsync()
        {
             try 
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<Guid>>("api/Notifications/Dismissed") ?? new List<Guid>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting dismissed items: {ex.Message}");
                return new List<Guid>();
            }
        }

        public async Task DismissAsync(NotificationDismissal dismissal)
        {
             try 
            {
                await _httpClient.PostAsJsonAsync("api/Notifications/Dismiss", dismissal);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error dismissing item: {ex.Message}");
            }
        }

        public async Task ClearAllAsync()
        {
            // Implementation depends on API support for bulk delete
            // For now, no-op or loop
             System.Diagnostics.Debug.WriteLine("ClearAllAsync not implemented fully in API yet.");
             await Task.CompletedTask;
        }

        public async Task SendReminderAsync(string title, string message, string? action = null)
        {
            var notif = new Notification
            {
                Title = title,
                Message = message,
                TargetAction = action,
                 Type = NotificationType.Reminder
            };
            await AddAsync(notif);
        }
    }
}
