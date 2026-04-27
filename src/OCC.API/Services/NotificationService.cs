using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;

namespace OCC.API.Services
{
    public interface INotificationService
    {
        Task RegisterDeviceAsync(Guid userId, string token, string platform, string? deviceName);
        Task SendPushNotificationAsync(Guid userId, string title, string body);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RegisterDeviceAsync(Guid userId, string token, string platform, string? deviceName)
        {
            var existing = await _context.UserDevices
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceToken == token);

            if (existing != null)
            {
                existing.LastSeenUtc = DateTime.UtcNow;
                existing.Platform = platform;
                existing.DeviceName = deviceName;
            }
            else
            {
                _context.UserDevices.Add(new UserDevice
                {
                    UserId = userId,
                    DeviceToken = token,
                    Platform = platform,
                    DeviceName = deviceName,
                    LastSeenUtc = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task SendPushNotificationAsync(Guid userId, string title, string body)
        {
            var devices = await _context.UserDevices
                .Where(d => d.UserId == userId)
                .ToListAsync();

            if (!devices.Any()) return;

            foreach (var device in devices)
            {
                // --- FINAL STEP: FIREBASE INTEGRATION ---
                try
                {
                    var message = new FirebaseAdmin.Messaging.Message()
                    {
                        Token = device.DeviceToken,
                        Notification = new FirebaseAdmin.Messaging.Notification()
                        {
                            Title = title,
                            Body = body
                        },
                        Data = new Dictionary<string, string>()
                        {
                            { "ClickAction", "OPEN_PROJECT" },
                            { "Timestamp", DateTime.UtcNow.ToString() }
                        }
                    };

                    string response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message);
                    Console.WriteLine($"[PUSH SENT] ID: {response} To User {userId} on {device.Platform}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PUSH FAILED] To User {userId} on {device.Platform}: {ex.Message}");
                }
            }
            
            await Task.CompletedTask;
        }
    }
}
