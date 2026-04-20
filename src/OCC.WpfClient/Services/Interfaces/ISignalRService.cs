using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.DTOs;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ISignalRService
    {
        event Action<List<UserConnectionInfo>> UserListUpdated;
        event Action<string> NotificationReceived;
        event Action<DashboardUpdateDto> DashboardUpdateReceived;
        
        bool IsConnected { get; }
        int OnlineCount { get; }
        List<UserConnectionInfo> OnlineUsers { get; }
        
        Task StartAsync();
        Task StopAsync();
        Task RestartAsync();
        Task UpdateStatusAsync(string status);
    }
}
