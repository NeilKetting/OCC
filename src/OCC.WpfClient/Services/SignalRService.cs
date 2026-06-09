using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace OCC.WpfClient.Services
{
    public class SignalRService : ISignalRService, IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private HubConnection? _chatHubConnection;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly ILogger<SignalRService> _logger;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly string _debugLogPath;
        private DateTime _lastTaskToastTime = DateTime.MinValue;

        public event Action<List<UserConnectionInfo>>? UserListUpdated;
        public event Action<string>? NotificationReceived;
        public event Action<DashboardUpdateDto>? DashboardUpdateReceived;
        public event Action<ChatMessageDto>? ChatMessageReceived;
        public event Action<Guid>? SessionDeleted;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public bool IsChatConnected => _chatHubConnection?.State == HubConnectionState.Connected;
        public int OnlineCount => OnlineUsers.Count;
        public List<UserConnectionInfo> OnlineUsers { get; private set; } = new();

        public SignalRService(ConnectionSettings connectionSettings, IAuthService authService, ILogger<SignalRService> logger)
        {
            _connectionSettings = connectionSettings;
            _authService = authService;
            _logger = logger;
            _debugLogPath = System.IO.Path.Combine(AppContext.BaseDirectory, "wpf-sync-debug.txt");
            
            _authService.UserChanged += async (s, e) => 
            {
                if (_authService.CurrentUser != null) await RestartAsync();
                else await StopAsync();
            };

            DebugLog("SignalR Service Created.");
        }

        private void DebugLog(string message)
        {
            _logger.LogInformation(message);
            try { System.IO.File.AppendAllText(_debugLogPath, $"[{DateTime.Now}] {message}\n"); } catch { }
        }

        public async Task StartAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected) return;
                if (_authService.CurrentToken == null) return;

                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                var hubUrl = $"{baseUrl}/hubs/notifications";
                DebugLog($"Connecting to SignalR Notification Hub at {hubUrl}...");

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_authService.CurrentToken);
                        options.Headers.Add("X-Environment", _connectionSettings.SelectedEnvironment.ToString());
                    })
                    .WithAutomaticReconnect()
                    .Build();

                RegisterListeners();

                await _hubConnection.StartAsync();
                DebugLog($"SignalR Notification Hub connected. ID: {_hubConnection.ConnectionId}");

                var chatHubUrl = $"{baseUrl}/hubs/chat";
                DebugLog($"Connecting to SignalR Chat Hub at {chatHubUrl}...");

                _chatHubConnection = new HubConnectionBuilder()
                    .WithUrl(chatHubUrl, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_authService.CurrentToken);
                        options.Headers.Add("X-Environment", _connectionSettings.SelectedEnvironment.ToString());
                    })
                    .WithAutomaticReconnect()
                    .Build();

                RegisterChatListeners();

                await _chatHubConnection.StartAsync();
                DebugLog($"SignalR Chat Hub connected. ID: {_chatHubConnection.ConnectionId}");

                // Show success toast on main UI
                App.Current.Dispatcher.Invoke(() => {
                    WeakReferenceMessenger.Default.Send(new OCC.WpfClient.Infrastructure.Messages.ToastNotificationMessage(
                        new Models.ToastMessage("Connected", "Real-time sync active", Models.ToastType.Success)));
                });
            }
            catch (Exception ex)
            {
                DebugLog($"Failed to connect to SignalR Hubs: {ex.Message}");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void RegisterListeners()
        {
            if (_hubConnection == null) return;

            _hubConnection.On<List<UserConnectionInfo>>("UserListUpdate", (users) =>
            {
                OnlineUsers = users ?? new List<UserConnectionInfo>();
                UserListUpdated?.Invoke(OnlineUsers);
            });

            _hubConnection.On<string>("ReceiveNotification", (message) =>
            {
                NotificationReceived?.Invoke(message);
            });

            _hubConnection.On<DashboardUpdateDto>("DashboardUpdate", (update) =>
            {
                DashboardUpdateReceived?.Invoke(update);
            });

            _hubConnection.On<string, string, string>("EntityUpdate", (entityType, action, idStr) =>
            {
                DebugLog($"[SignalR] RECV EntityUpdate: {entityType} | {action} | {idStr}");

                if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out Guid id)) return;

                App.Current.Dispatcher.Invoke(() => {
                    // Debounce by 1 second to handle parent rollups sending multiple events
                    if (entityType == "ProjectTask" && (DateTime.Now - _lastTaskToastTime).TotalSeconds > 1)
                    {
                        _lastTaskToastTime = DateTime.Now;
                    }

                    // Send specific messages for Desktop ViewModels that listen for them
                    if (entityType == "ProjectTask")
                    {
                        WeakReferenceMessenger.Default.Send(new OCC.WpfClient.Infrastructure.Messages.TaskUpdatedMessage(id));
                    }
                    else if (entityType == "Project")
                    {
                        // Silent refresh for project data (no toast)
                        WeakReferenceMessenger.Default.Send(new OCC.WpfClient.Infrastructure.Messages.ProjectUpdatedMessage(id));
                    }
                });
            });
        }

        private void RegisterChatListeners()
        {
            if (_chatHubConnection == null) return;

            _chatHubConnection.On<ChatMessageDto>("ReceiveMessage", (messageDto) =>
            {
                ChatMessageReceived?.Invoke(messageDto);
            });

            _chatHubConnection.On<Guid>("SessionDeleted", (sessionId) =>
            {
                SessionDeleted?.Invoke(sessionId);
            });
        }

        public async Task StopAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_hubConnection != null)
                {
                    await _hubConnection.StopAsync();
                    await _hubConnection.DisposeAsync();
                    _hubConnection = null;
                }
                if (_chatHubConnection != null)
                {
                    await _chatHubConnection.StopAsync();
                    await _chatHubConnection.DisposeAsync();
                    _chatHubConnection = null;
                }
            }
            finally
            {
                _connectionLock.Release();
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

        public async Task SendChatMessageAsync(Guid sessionId, string content)
        {
            if (_chatHubConnection != null && _chatHubConnection.State == HubConnectionState.Connected)
            {
                await _chatHubConnection.InvokeAsync("SendMessage", sessionId, content);
            }
            else
            {
                throw new InvalidOperationException("Chat hub is not connected.");
            }
        }

        public async Task MarkChatSessionAsReadAsync(Guid sessionId)
        {
            if (_chatHubConnection != null && _chatHubConnection.State == HubConnectionState.Connected)
            {
                await _chatHubConnection.InvokeAsync("MarkSessionAsRead", sessionId);
            }
        }

        public async Task<bool> ToggleChatFavouriteAsync(Guid sessionId)
        {
            if (_chatHubConnection != null && _chatHubConnection.State == HubConnectionState.Connected)
            {
                return await _chatHubConnection.InvokeAsync<bool>("ToggleFavourite", sessionId);
            }
            return false;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
