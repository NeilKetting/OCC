using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using OCC.Mobile.Services;
using OCC.Shared.Models;
using Serilog;

namespace OCC.Mobile.Services
{
    public interface ISignalRService
    {
        Task StartAsync();
        Task StopAsync();
        bool IsConnected { get; }
        event Action<string, string, Guid>? EntityUpdated;
    }

    public class SignalRService : ISignalRService, IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly IAuthService _authService;
        private readonly ILocalSettingsService _settingsService;

        public event Action<string, string, Guid>? EntityUpdated;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SignalRService(IAuthService authService, ILocalSettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "http://102.221.36.149:8081/";
        }

        public async Task StartAsync()
        {
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected) return;
            if (string.IsNullOrEmpty(_authService.CurrentToken)) return;

            var baseUrl = GetBaseUrl();
            var hubUrl = $"{baseUrl}hubs/notifications";
            Serilog.Log.Information("[SignalR-Mobile] Initializing Connection. URL: {Url}", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_authService.CurrentToken);
                })
                .WithAutomaticReconnect()
                .Build();

             _hubConnection.On<string, string, string>("EntityUpdate", (entityType, action, idStr) =>
            {
                if (Guid.TryParse(idStr, out Guid id))
                {
                    Serilog.Log.Information("[SignalR-Mobile] RECV EntityUpdate: {Type} {Action} {Id}", entityType, action, id);
                    EntityUpdated?.Invoke(entityType, action, id);
                }
            });
            
            _hubConnection.On<DateTime>("Heartbeat", (timestamp) =>
            {
                Serilog.Log.Debug("[SignalR-Mobile] RECV Heartbeat: {Timestamp}", timestamp);
            });

            try
            {
                Serilog.Log.Information("[SignalR-Mobile] Attempting to StartAsync...");
                await _hubConnection.StartAsync();
                Serilog.Log.Information("[SignalR-Mobile] StartAsync Success. ConnectionId: {Id}", _hubConnection.ConnectionId);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[SignalR-Mobile] StartAsync FATAL ERROR");
            }
        }

        public async Task StopAsync()
        {
            if (_hubConnection != null)
            {
                Serilog.Log.Information("[SignalR-Mobile] Stopping connection...");
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
