using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure.Messages;
using OCC.Shared.DTOs;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;

namespace OCC.WpfClient.Features.MainHub.ViewModels
{
    public partial class ChatsWidgetViewModel : WidgetViewModelBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly ILocalEncryptionService _encryptionService;

        [ObservableProperty]
        private bool _isLoadingChats;

        public ObservableCollection<ChatSessionDto> UnreadSessions { get; } = new();

        public ChatsWidgetViewModel(IHttpClientFactory httpClientFactory, ConnectionSettings connectionSettings, IAuthService authService, ILocalEncryptionService encryptionService)
        {
            _httpClientFactory = httpClientFactory;
            _connectionSettings = connectionSettings;
            _authService = authService;
            _encryptionService = encryptionService;
            WidgetId = "Chats";
            Title = "Unread Chats";
        }

        [RelayCommand]
        private void NavigateToChatSession(ChatSessionDto? session)
        {
            if (session == null) return;
            WeakReferenceMessenger.Default.Send(new OpenChatSessionMessage(session.Id));
        }

        [RelayCommand]
        private void NavigateToChatHub()
        {
            WeakReferenceMessenger.Default.Send(new OpenHubMessage("Chat"));
        }

        public override async Task RefreshDataAsync()
        {
            if (_authService.CurrentToken == null) return;
            if (IsLoadingChats) return;
            IsLoadingChats = true;
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
                
                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                var sessions = await client.GetFromJsonAsync<ChatSessionDto[]>($"{baseUrl}/api/messages/sessions");
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    UnreadSessions.Clear();
                    if (sessions != null)
                    {
                        var unreadList = sessions.Where(s => s.UnreadCount > 0).OrderByDescending(s => s.LastMessage?.SentDate ?? s.CreatedAtUtc).ToList();
                        foreach (var s in unreadList)
                        {
                            if (!s.IsGroupChat && string.IsNullOrEmpty(s.Name))
                            {
                                var otherUser = s.Users.FirstOrDefault(u => u.UserId != _authService.CurrentUser?.Id);
                                if (otherUser != null)
                                {
                                    s.Name = $"{otherUser.FirstName} {otherUser.LastName}";
                                }
                            }
                            if (s.LastMessage != null && !s.LastMessage.HasAttachment && !string.IsNullOrEmpty(s.SharedAesKey))
                            {
                                s.LastMessage.Content = _encryptionService.DecryptMessage(s.LastMessage.Content, s.SharedAesKey);
                            }
                            UnreadSessions.Add(s);
                        }
                    }
                });
            }
            catch { }
            finally
            {
                IsLoadingChats = false;
            }
        }
    }
}
