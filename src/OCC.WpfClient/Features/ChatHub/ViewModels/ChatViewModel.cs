using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using OCC.Shared.DTOs;
using OCC.WpfClient.Features.ChatHub.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.Logging;

namespace OCC.WpfClient.Features.ChatHub.ViewModels
{
    public enum ChatFilter
    {
        All,
        Unread,
        Favourites
    }

    public partial class ChatViewModel : ViewModelBase, IAsyncDisposable
    {
        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly IAuthService _authService;
        private readonly ConnectionSettings _connectionSettings;
        private readonly HttpClient _httpClient;
        private readonly ILocalEncryptionService _encryptionService;
        private readonly ILogger<ChatViewModel> _logger;
        private readonly IDialogService _dialogService;
        private readonly ISignalRService _signalRService;

        [ObservableProperty]
        private ObservableCollection<ChatSessionModel> _chatSessions = new();

        private Guid? _pendingSessionIdToSelect;

        public ICollectionView SessionsView { get; }

        [ObservableProperty]
        private bool _isAllFilterSelected = true;

        [ObservableProperty]
        private bool _isUnreadFilterSelected;

        [ObservableProperty]
        private bool _isFavouritesFilterSelected;

        private ChatFilter _currentFilter = ChatFilter.All;

        private ChatSessionModel? _selectedSession;
        public ChatSessionModel? SelectedSession
        {
            get => _selectedSession;
            set
            {
                if (SetProperty(ref _selectedSession, value))
                {
                    if (value != null)
                    {
                        if (value.UnreadCount > 0)
                        {
                            value.UnreadCount = 0;
                            _ = MarkSessionAsReadAsync(value.Id);
                        }
                        _ = LoadMessagesForSessionAsync(value);
                    }
                }
            }
        }

        public event EventHandler? RequestClearInput;

        private string _messageInput = string.Empty;
        public string MessageInput
        {
            get => _messageInput;
            set 
            {
                if (SetProperty(ref _messageInput, value))
                {
                    // Minimal logging here to avoid flood, but enough to see it's working
                    if (!string.IsNullOrEmpty(value))
                        Debug.WriteLine($"[ChatVM {_instanceId}] Input actual: '{value}'");
                }
            }
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    SessionsView.Refresh();
                    UpdateUserSearchResults();
                }
            }
        }

        [ObservableProperty]
        private ObservableCollection<ChatUserDto> _userSearchResults = new();

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isNewChatVisible;

        [ObservableProperty]
        private bool _isSelectionMode;

        [ObservableProperty]
        private bool _isGroupDetailsVisible;

        [ObservableProperty]
        private string _groupSubject = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ChatUserDto> _selectedContacts = new();

        public Guid CurrentUserId => _authService.CurrentUser?.Id ?? Guid.Empty;

        [ObservableProperty]
        private string _searchContactsText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ChatUserDto> _availableContacts = new();

        public ICollectionView ContactsView { get; }

        [ObservableProperty]
        private ChatMessageModel? _replyingToMessage;

        [ObservableProperty]
        private ChatMessageModel? _forwardingMessage;

        [ObservableProperty]
        private bool _isForwardingPopupOpen;

        public ChatViewModel(IAuthService authService,
                             ConnectionSettings connectionSettings,
                             IHttpClientFactory httpClientFactory,
                             ILocalEncryptionService encryptionService,
                             ILogger<ChatViewModel> logger,
                             IDialogService dialogService,
                             ISignalRService signalRService)
        {
            _authService = authService;
            _connectionSettings = connectionSettings;
            _httpClient = httpClientFactory.CreateClient();
            _encryptionService = encryptionService;
            _logger = logger;
            _dialogService = dialogService;
            _signalRService = signalRService;

            // Add authorization header for HTTP requests
            if (!string.IsNullOrEmpty(_authService.CurrentToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
            }

            Title = "Chat";
            Debug.WriteLine($"[ChatVM {_instanceId}] Constructor initialized. Thread ID: {Environment.CurrentManagedThreadId}");
            
            SessionsView = CollectionViewSource.GetDefaultView(ChatSessions);
            SessionsView.Filter = FilterSessions;
            SessionsView.SortDescriptions.Add(new SortDescription(nameof(ChatSessionModel.LastMessageTime), ListSortDirection.Descending));

            ContactsView = CollectionViewSource.GetDefaultView(AvailableContacts);
            ContactsView.Filter = FilterContacts;
            
            // Add sorting and grouping by name
            ContactsView.SortDescriptions.Add(new SortDescription(nameof(ChatUserDto.FirstName), ListSortDirection.Ascending));
            ContactsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ChatUserDto.FirstName), new Infrastructure.Converters.FirstLetterConverter()));

            // Initialize in background
            _ = InitializeAsync();
        }

        private bool FilterContacts(object item)
        {
            if (item is ChatUserDto contact)
            {
                if (string.IsNullOrWhiteSpace(SearchContactsText)) return true;
                var term = SearchContactsText.ToLower();
                return contact.FirstName.ToLower().Contains(term) || 
                       contact.LastName.ToLower().Contains(term) ||
                       contact.Email.ToLower().Contains(term);
            }
            return false;
        }

        partial void OnSearchContactsTextChanged(string value)
        {
            ContactsView.Refresh();
        }

        private void UpdateUserSearchResults()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                UserSearchResults.Clear();
                return;
            }

            var term = SearchQuery.ToLower();
            var results = AvailableContacts
                .Where(u => u.UserId != CurrentUserId &&
                           (u.FirstName.ToLower().Contains(term) || 
                            u.LastName.ToLower().Contains(term) || 
                            u.Email.ToLower().Contains(term)))
                .Take(10)
                .ToList();

            UserSearchResults.Clear();
            foreach (var user in results)
            {
                // Optionally filter out users who already have an active session showing in the list
                UserSearchResults.Add(user);
            }
        }

        private bool FilterSessions(object item)
        {
            if (item is ChatSessionModel session)
            {
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var term = SearchQuery.ToLower();
                    if (!session.Name.ToLower().Contains(term)) return false;
                }

                if (_currentFilter == ChatFilter.Unread) return session.UnreadCount > 0;
                if (_currentFilter == ChatFilter.Favourites) return session.IsFavourite;
                return true;
            }
            return false;
        }

        [RelayCommand]
        private void SetFilter(string filterString)
        {
            if (Enum.TryParse<ChatFilter>(filterString, out var filter))
            {
                _currentFilter = filter;
                IsAllFilterSelected = _currentFilter == ChatFilter.All;
                IsUnreadFilterSelected = _currentFilter == ChatFilter.Unread;
                IsFavouritesFilterSelected = _currentFilter == ChatFilter.Favourites;
                SessionsView.Refresh();
            }
        }

        private async Task MarkSessionAsReadAsync(Guid sessionId)
        {
            if (_signalRService.IsChatConnected)
            {
                try
                {
                    await _signalRService.MarkChatSessionAsReadAsync(sessionId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to mark session as read: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task ShowNewChatAsync()
        {
            IsNewChatVisible = true;
            IsSelectionMode = false;
            await LoadContactsAsync();
        }

        [RelayCommand]
        private async Task ShowAddGroupMembersAsync()
        {
            IsNewChatVisible = true;
            IsSelectionMode = true;
            SelectedContacts.Clear();
            await LoadContactsAsync();
        }

        [RelayCommand]
        private void HideNewChat()
        {
            IsNewChatVisible = false;
            IsSelectionMode = false;
            IsGroupDetailsVisible = false;
            SelectedContacts.Clear();
            GroupSubject = string.Empty;
        }

        [RelayCommand]
        private void GoToGroupDetails() => IsGroupDetailsVisible = true;

        [RelayCommand]
        private void BackToGroupMembers() => IsGroupDetailsVisible = false;

        private async Task LoadContactsAsync()
        {
            try
            {
                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                var contacts = await _httpClient.GetFromJsonAsync<ChatUserDto[]>($"{baseUrl}/api/users/contacts");
                
                if (contacts != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableContacts.Clear();
                        foreach (var dto in contacts)
                        {
                            AvailableContacts.Add(dto);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load contacts: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task StartNewChatAsync(ChatUserDto contact)
        {
            if (contact != null)
            {
                await StartDirectChatAsync(contact.UserId);
            }
        }

        [RelayCommand]
        private async Task StartNewChatFromSearchAsync(ChatUserDto contact)
        {
            if (contact != null)
            {
                var targetUserId = contact.UserId;
                SearchQuery = string.Empty; // Clear search
                await StartDirectChatAsync(targetUserId);
            }
        }

        [RelayCommand]
        private async Task ToggleFavouriteAsync(ChatSessionModel session)
        {
            if (session == null || !_signalRService.IsChatConnected) return;

            try
            {
                var isFav = await _signalRService.ToggleChatFavouriteAsync(session.Id);
                session.IsFavourite = isFav;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to toggle favourite: {ex.Message}");
            }
        }

        private void OnGlobalChatMessageReceived(ChatMessageDto messageDto)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                HandleIncomingMessage(messageDto);
            });
        }

        private void OnGlobalSessionDeleted(Guid sessionId)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var session = ChatSessions.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    ChatSessions.Remove(session);
                    if (SelectedSession?.Id == sessionId) SelectedSession = null;
                    SessionsView.Refresh();
                }
            });
        }

        private async Task InitializeAsync()
        {
            await LoadSessionsAsync();
            await LoadContactsAsync(); // Load all contacts for search
            
            _signalRService.ChatMessageReceived += OnGlobalChatMessageReceived;
            _signalRService.SessionDeleted += OnGlobalSessionDeleted;
            IsConnected = _signalRService.IsChatConnected;
        }

        private async Task LoadSessionsAsync()
        {
            try
            {
                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                var sessions = await _httpClient.GetFromJsonAsync<ChatSessionDto[]>($"{baseUrl}/api/messages/sessions");

                if (sessions != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ChatSessions.Clear();
                        foreach (var dto in sessions)
                        {
                            try
                            {
                                var model = new ChatSessionModel(dto, CurrentUserId);

                                // Use Shared AES Key from server
                                model.DecryptedAesKey = dto.SharedAesKey;

                                // Decrypt LastMessagePreview if there's a key
                                if (!string.IsNullOrEmpty(model.DecryptedAesKey) && dto.LastMessage != null && !dto.LastMessage.HasAttachment)
                                {
                                    try
                                    {
                                        model.LastMessagePreview = _encryptionService.DecryptMessage(dto.LastMessage.Content, model.DecryptedAesKey);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Failed to decrypt message for session {SessionId}", dto.Id);
                                    }
                                }

                                model.IsCurrentUserAdmin = model.IsGroupChat && model.CreatedById == CurrentUserId;

                                ChatSessions.Add(model);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing chat session {SessionId}", dto.Id);
                            }
                        }

                        if (_pendingSessionIdToSelect.HasValue)
                        {
                            var pendingSession = ChatSessions.FirstOrDefault(s => s.Id == _pendingSessionIdToSelect.Value);
                            if (pendingSession != null)
                            {
                                SelectedSession = pendingSession;
                            }
                            _pendingSessionIdToSelect = null;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load sessions: {ex.Message}");
            }
        }

        public void SelectSessionById(Guid sessionId)
        {
            var session = ChatSessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                SelectedSession = session;
                _pendingSessionIdToSelect = null;
            }
            else
            {
                _pendingSessionIdToSelect = sessionId;
            }
        }



        private void HandleIncomingMessage(ChatMessageDto messageDto)
        {
            _logger.LogInformation("ReceiveMessage: From={Sender}, Session={SessionId}, HasAttachment={HasAttachment}", 
                messageDto.SenderName, messageDto.ChatSessionId, messageDto.HasAttachment);
            
            var session = ChatSessions.FirstOrDefault(s => s.Id == messageDto.ChatSessionId);
            if (session != null)
            {
                App.Current.Dispatcher.Invoke(() => 
                {
                    // Update preview immediately for the session list
                    session.LastMessagePreview = messageDto.HasAttachment ? "📎 Attachment" : messageDto.Content;
                    session.LastMessageTime = messageDto.SentDate;
                    
                    // Force a refresh of the view to re-sort and update the preview on the left
                    SessionsView.Refresh();
                    
                    Debug.WriteLine($"[ChatVM {_instanceId}] Updated session preview and refreshed view for {session.Id}");
                });

                // Add message if session is active
                if (SelectedSession?.Id == session.Id && _authService.CurrentUser != null)
                {
                    // Decrypt incoming message content
                    try 
                    {
                        if (!string.IsNullOrEmpty(session.DecryptedAesKey) && !messageDto.HasAttachment)
                        {
                            messageDto.Content = _encryptionService.DecryptMessage(messageDto.Content, session.DecryptedAesKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Decryption failed for incoming message: {ex.Message}");
                    }

                    var msgModel = new ChatMessageModel(messageDto, _authService.CurrentUser.Id);
                    
                    App.Current.Dispatcher.Invoke(() => 
                    {
                        session.Messages.Add(msgModel);
                        Debug.WriteLine($"Added message to session {session.Id}. Total messages: {session.Messages.Count}");
                    });
                }
                else
                {
                    // Even if not selected, we may need to decrypt it for the preview
                    if (!string.IsNullOrEmpty(session.DecryptedAesKey) && !messageDto.HasAttachment)
                    {
                        messageDto.Content = _encryptionService.DecryptMessage(messageDto.Content, session.DecryptedAesKey);
                    }
                }

                // Update preview
                session.LastMessagePreview = messageDto.HasAttachment ? "📎 Attachment" : messageDto.Content;
                session.LastMessageTime = messageDto.SentDate;
            }
            // else: Handle new session created by incoming message (refresh sessions)
        }



        private async Task LoadMessagesForSessionAsync(ChatSessionModel session)
        {
            if (_authService.CurrentUser == null) return;

            try
            {
                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                // Get last 50 messages
                var messages = await _httpClient.GetFromJsonAsync<ChatMessageDto[]>($"{baseUrl}/api/messages/sessions/{session.Id}/messages?take=50");

                if (messages != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        session.Messages.Clear();
                        foreach (var dto in messages)
                        {
                            if (!string.IsNullOrEmpty(session.DecryptedAesKey) && !dto.HasAttachment)
                            {
                                dto.Content = _encryptionService.DecryptMessage(dto.Content, session.DecryptedAesKey);
                            }
                            session.Messages.Add(new ChatMessageModel(dto, _authService.CurrentUser.Id));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load messages for session {session.Id}: {ex.Message}");
            }
        }

        public async Task StartDirectChatAsync(Guid targetUserId)
        {
            if (_authService.CurrentUser == null || targetUserId == Guid.Empty) return;

            try
            {
                var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                
                // Simple session creation (Server handles keys)
                // Pass dummy payload to satisfy legacy server-side validation on existing deployments
                var payload = new { MyEncryptedAesKey = "dummy", TargetEncryptedAesKey = "dummy" };
                var checkResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/messages/direct/{targetUserId}", payload);
                if (checkResponse.IsSuccessStatusCode)
                {
                    var result = await checkResponse.Content.ReadFromJsonAsync<DirectSessionResponse>();
                    if (result?.SessionId != null)
                    {
                        await LoadSessionsAsync();
                        SelectedSession = ChatSessions.FirstOrDefault(s => s.Id == result.SessionId);
                    }
                }

                // Close the overlay
                IsNewChatVisible = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting direct chat: {ex.Message}");
                await _dialogService.ShowAlertAsync("Error", "Failed to initiate chat session. Please try again or check your connection.");
            }
        }

        private class DirectSessionResponse 
        {
            public Guid? SessionId { get; set; }
            public bool RequiresKeys { get; set; }
        }

        private class PublicKeyResponse
        {
            public string PublicKey { get; set; } = string.Empty;
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            _logger.LogInformation("SendMessageAsync started. Session: {SessionId}, Connection: {ConnectionState}", 
                SelectedSession?.Id, _signalRService.IsChatConnected);
            
            if (SelectedSession == null || string.IsNullOrWhiteSpace(MessageInput))
            {
                _logger.LogWarning("SendMessage blocked: Session is null or input is empty.");
                return;
            }

            if (!_signalRService.IsChatConnected)
            {
                _logger.LogWarning("SendMessage blocked: SignalR Chat not connected.");
                _ = _signalRService.RestartAsync();
                return;
            }

            var plainContent = MessageInput;
            if (ReplyingToMessage != null)
            {
                var snippet = ReplyingToMessage.DisplayContent;
                if (snippet.Length > 60)
                {
                    snippet = snippet.Substring(0, 57) + "...";
                }
                snippet = snippet.Replace("\n", " ");
                plainContent = $"[Reply to {ReplyingToMessage.SenderName}: {snippet}]:\n{plainContent}";
            }
            bool success = false;

            try
            {
                var contentToSend = plainContent;
                if (!string.IsNullOrEmpty(SelectedSession.DecryptedAesKey))
                {
                    contentToSend = _encryptionService.EncryptMessage(plainContent, SelectedSession.DecryptedAesKey);
                }

                _logger.LogDebug("Invoking SendMessage on Hub for session {SessionId}", SelectedSession.Id);
                await _signalRService.SendChatMessageAsync(SelectedSession.Id, contentToSend);
                _logger.LogInformation("Message sent successfully via Hub.");
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message via SignalR hub.");
            }
            finally
            {
                if (success)
                {
                    App.Current.Dispatcher.Invoke(() => {
                        MessageInput = string.Empty;
                        ReplyingToMessage = null;
                        RequestClearInput?.Invoke(this, EventArgs.Empty);
                    });
                }
                else
                {
                    _logger.LogWarning("Message send failed. Input preserved.");
                }
            }
        }

        [RelayCommand]
        private async Task DeleteSessionAsync(ChatSessionModel session)
        {
            if (session == null) return;

            // Confirm delete
            bool isAdmin = session.IsAdmin(_authService.CurrentUser?.Id ?? Guid.Empty);
            
            if (session.IsGroupChat && !isAdmin)
            {
                await _dialogService.ShowAlertAsync("Permission Denied", "Only the group creator can delete this group.");
                return;
            }

            string title = session.IsGroupChat ? "Delete Group" : "Delete Chat";
            string message = session.IsGroupChat 
                ? $"Are you sure you want to delete the group \"{session.Name}\" for everyone?\n\nThis action cannot be undone." 
                : $"Are you sure you want to delete your chat with \"{session.Name}\"?";

            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);

            if (!confirmed) return;

            try
            {
                var response = await _httpClient.DeleteAsync($"{_connectionSettings.ApiBaseUrl.TrimEnd('/')}/api/messages/sessions/{session.Id}");
                if (response.IsSuccessStatusCode)
                {
                    App.Current.Dispatcher.Invoke(() => {
                        ChatSessions.Remove(session);
                        if (SelectedSession == session) SelectedSession = null;
                        SessionsView.Refresh();
                    });
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Error", $"Failed to delete { (session.IsGroupChat ? "group" : "chat") }.\n\nServer returned: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session {SessionId}", session.Id);
                await _dialogService.ShowAlertAsync("Error", $"An error occurred while deleting the { (session.IsGroupChat ? "group" : "chat") }.\n\n{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ExitGroupAsync(ChatSessionModel session)
        {
            if (session == null || !session.IsGroupChat) return;

            var confirmed = await _dialogService.ShowConfirmationAsync("Exit Group", "Are you sure you want to exit this group?");
            if (!confirmed) return;

            try
            {
                var response = await _httpClient.PostAsync($"{_connectionSettings.ApiBaseUrl.TrimEnd('/')}/api/messages/sessions/{session.Id}/exit", null);
                if (response.IsSuccessStatusCode)
                {
                    ChatSessions.Remove(session);
                    if (SelectedSession == session) SelectedSession = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to exit group: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleSelectionMode()
        {
            IsSelectionMode = !IsSelectionMode;
            SelectedContacts.Clear();
        }

        [RelayCommand]
        private void ToggleContactSelection(ChatUserDto contact)
        {
            if (contact == null) return;
            if (SelectedContacts.Contains(contact))
                SelectedContacts.Remove(contact);
            else
                SelectedContacts.Add(contact);
        }

        [RelayCommand]
        private async Task CreateGroupAsync(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName) || SelectedContacts.Count < 1) return;

            try
            {
                // 1. Prepare participants (creator + selected)
                var participants = new List<object>();
                
                // Add self
                participants.Add(new { UserId = CurrentUserId });

                // Add others
                foreach (var contact in SelectedContacts)
                {
                    participants.Add(new { UserId = contact.UserId });
                }

                // 3. Send to API
                var baseUrlFinal = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                var request = new { Name = groupName, Participants = participants };
                var response = await _httpClient.PostAsJsonAsync($"{baseUrlFinal}/api/messages/groups", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DirectSessionResponse>();
                    if (result?.SessionId != null)
                    {
                        await LoadSessionsAsync();
                        SelectedSession = ChatSessions.FirstOrDefault(s => s.Id == result.SessionId);
                        IsNewChatVisible = false;
                        IsSelectionMode = false;
                        SelectedContacts.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create group: {ex.Message}");
                await _dialogService.ShowAlertAsync("Error", "Failed to create group. Please try again.");
            }
        }

        [RelayCommand]
        private void ArchiveSession(ChatSessionModel session) => Debug.WriteLine($"Archive: {session.Name}");

        [RelayCommand]
        private void MuteSession(ChatSessionModel session) => Debug.WriteLine($"Mute: {session.Name}");

        [RelayCommand]
        private void PinSession(ChatSessionModel session) => Debug.WriteLine($"Pin: {session.Name}");

        [RelayCommand]
        private void MarkAsRead(ChatSessionModel session) => Debug.WriteLine($"Mark as Read: {session.Name}");

        [RelayCommand]
        private void AddToList(ChatSessionModel session) => Debug.WriteLine($"Add to List: {session.Name}");

        [RelayCommand]
        private void ClearSession(ChatSessionModel session) => Debug.WriteLine($"Clear: {session.Name}");

        [RelayCommand]
        private void RemoveContact(ChatUserDto contact) => SelectedContacts.Remove(contact);

        [RelayCommand]
        private void ReplyMessage(ChatMessageModel message)
        {
            if (message == null) return;
            ReplyingToMessage = message;
        }

        [RelayCommand]
        private void CancelReply()
        {
            ReplyingToMessage = null;
        }

        [RelayCommand]
        private void ForwardMessage(ChatMessageModel message)
        {
            if (message == null) return;
            ForwardingMessage = message;
            IsForwardingPopupOpen = true;
        }

        [RelayCommand]
        private void CancelForward()
        {
            ForwardingMessage = null;
            IsForwardingPopupOpen = false;
        }

        [RelayCommand]
        private async Task ConfirmForwardAsync(ChatSessionModel targetSession)
        {
            if (targetSession == null || ForwardingMessage == null) return;

            var plainContent = $"[Forwarded]:\n{ForwardingMessage.DisplayContent}";

            try
            {
                var contentToSend = plainContent;
                if (!string.IsNullOrEmpty(targetSession.DecryptedAesKey))
                {
                    contentToSend = _encryptionService.EncryptMessage(plainContent, targetSession.DecryptedAesKey);
                }

                await _signalRService.SendChatMessageAsync(targetSession.Id, contentToSend);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward message.");
                await _dialogService.ShowAlertAsync("Error", "Failed to forward message. Please check connection.");
            }
            finally
            {
                ForwardingMessage = null;
                IsForwardingPopupOpen = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _signalRService.ChatMessageReceived -= OnGlobalChatMessageReceived;
            _signalRService.SessionDeleted -= OnGlobalSessionDeleted;
            await Task.CompletedTask;
        }
    }
}
