using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class SignalRService : ISignalRService, IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly ILogger<SignalRService> _logger;
        
        public event Action<List<UserConnectionInfo>>? UserListUpdated;
        public event Action<string>? NotificationReceived;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public int OnlineCount => OnlineUsers.Count;
        public List<UserConnectionInfo> OnlineUsers { get; private set; } = new();

        public SignalRService(ConnectionSettings connectionSettings, IAuthService authService, ILogger<SignalRService> logger)
        {
            _connectionSettings = connectionSettings;
            _authService = authService;
            _logger = logger;
            
            _authService.UserChanged += async (s, e) => 
            {
                if (_authService.CurrentUser != null) await RestartAsync();
                else await StopAsync();
            };
        }

        public async Task StartAsync()
        {
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected) return;
            if (_authService.CurrentToken == null) return;

            var hubUrl = $"{_connectionSettings.ApiBaseUrl.TrimEnd('/')}/hubs/notifications";
            _logger.LogInformation("Connecting to SignalR Notification Hub at {HubUrl}...", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_authService.CurrentToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<List<UserConnectionInfo>>("UserListUpdate", (users) =>
            {
                OnlineUsers = users ?? new List<UserConnectionInfo>();
                UserListUpdated?.Invoke(OnlineUsers);
            });

            _hubConnection.On<string>("ReceiveNotification", (message) =>
            {
                NotificationReceived?.Invoke(message);
            });

            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("SignalR Notification Hub connected.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SignalR Notification Hub.");
            }
        }

        public async Task StopAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task UpdateStatusAsync(string status)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("UpdateStatus", status);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
