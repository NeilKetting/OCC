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
        private HubConnection? _hubConnection;

        [ObservableProperty]
        private ObservableCollection<ChatSessionModel> _chatSessions = new();

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

        public ChatViewModel(IAuthService authService,
                             ConnectionSettings connectionSettings,
                             IHttpClientFactory httpClientFactory,
                             ILocalEncryptionService encryptionService,
                             ILogger<ChatViewModel> logger)
        {
            _authService = authService;
            _connectionSettings = connectionSettings;
            _httpClient = httpClientFactory.CreateClient();
            _encryptionService = encryptionService;
            _logger = logger;

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
            if (_hubConnection != null && IsConnected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("MarkSessionAsRead", sessionId);
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
            if (session == null || _hubConnection == null || !IsConnected) return;

            try
            {
                var isFav = await _hubConnection.InvokeAsync<bool>("ToggleFavourite", session.Id);
                session.IsFavourite = isFav;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to toggle favourite: {ex.Message}");
            }
        }

        private async Task InitializeAsync()
        {
            await LoadSessionsAsync();
            await LoadContactsAsync(); // Load all contacts for search
            await ConnectToSignalRAsync();
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

                                // Decrypt AES Key for this session
                                var myUserDto = dto.Users.FirstOrDefault(u => u.UserId == _authService.CurrentUser?.Id);
                                if (myUserDto != null && !string.IsNullOrEmpty(myUserDto.EncryptedAesKey))
                                {
                                    try
                                    {
                                        model.DecryptedAesKey = _encryptionService.DecryptAesKeyWithRsa(myUserDto.EncryptedAesKey);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Failed to decrypt AES key for session {SessionId}", dto.Id);
                                        model.LastMessagePreview = "[Encryption Error: Missing or Invalid Key]";
                                    }
                                }

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
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load sessions: {ex.Message}");
            }
        }

        private async Task ConnectToSignalRAsync()
        {
            if (_authService.CurrentToken == null) return;

            var hubUrl = $"{_connectionSettings.ApiBaseUrl.TrimEnd('/')}/hubs/chat";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_authService.CurrentToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<ChatMessageDto>("ReceiveMessage", (messageDto) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    HandleIncomingMessage(messageDto);
                });
            });

            _hubConnection.On<Guid, Guid>("MessageRead", (messageId, userId) =>
            {
                // Handle read receipts
            });

            _hubConnection.On<Guid>("SessionDeleted", (sessionId) =>
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
            });

            try
            {
                _logger.LogInformation("Connecting to SignalR Chat Hub at {HubUrl}...", hubUrl);
                await _hubConnection.StartAsync();
                IsConnected = true;
                _logger.LogInformation("Successfully connected to Chat Hub.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SignalR Chat Hub.");
                IsConnected = false;
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
                
                // Step 1: Check if session exists
                var checkResponse = await _httpClient.PostAsync($"{baseUrl}/api/messages/direct/{targetUserId}", null);
                if (!checkResponse.IsSuccessStatusCode) return;

                var result = await checkResponse.Content.ReadFromJsonAsync<DirectSessionResponse>();

                if (result?.RequiresKeys == true)
                {
                    // Step 2: Fetch Target Public Key
                    var keyResponse = await _httpClient.GetFromJsonAsync<PublicKeyResponse>($"{baseUrl}/api/users/{targetUserId}/public-key");
                    
                    if (keyResponse != null && !string.IsNullOrEmpty(keyResponse.PublicKey))
                    {
                        // Step 3: Generate and Encrypt AES Key
                        var rawAesKey = _encryptionService.GenerateAesKey();
                        var myEncryptedKey = _encryptionService.EncryptAesKeyWithRsa(rawAesKey, _encryptionService.GetPublicKey());
                        var targetEncryptedKey = _encryptionService.EncryptAesKeyWithRsa(rawAesKey, keyResponse.PublicKey);

                        // Step 4: Create Session with Keys
                        var createRequest = new { MyEncryptedAesKey = myEncryptedKey, TargetEncryptedAesKey = targetEncryptedKey };
                        var createResponse = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/messages/direct/{targetUserId}", createRequest);
                        
                        if (createResponse.IsSuccessStatusCode)
                        {
                            var createResult = await createResponse.Content.ReadFromJsonAsync<DirectSessionResponse>();
                            if (createResult?.SessionId != null)
                            {
                                await LoadSessionsAsync();
                                SelectedSession = ChatSessions.FirstOrDefault(s => s.Id == createResult.SessionId);
                            }
                        }
                    }
                }
                else if (result?.SessionId != null)
                {
                    // Session already exists
                    SelectedSession = ChatSessions.FirstOrDefault(s => s.Id == result.SessionId);
                }

                // Close the overlay
                IsNewChatVisible = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting direct chat: {ex.Message}");
                System.Windows.MessageBox.Show("Failed to initiate chat session. Please try again or check your connection.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                SelectedSession?.Id, _hubConnection?.State);
            
            if (SelectedSession == null || string.IsNullOrWhiteSpace(MessageInput))
            {
                _logger.LogWarning("SendMessage blocked: Session is null or input is empty.");
                return;
            }

            if (_hubConnection == null || !IsConnected || _hubConnection.State != HubConnectionState.Connected)
            {
                _logger.LogWarning("SendMessage blocked: SignalR not connected. State: {State}", _hubConnection?.State);
                // Attempt to reconnect if disconnected
                if (_hubConnection != null && _hubConnection.State == HubConnectionState.Disconnected)
                {
                    _ = ConnectToSignalRAsync();
                }
                return;
            }

            var plainContent = MessageInput;
            bool success = false;

            try
            {
                var contentToSend = plainContent;
                if (!string.IsNullOrEmpty(SelectedSession.DecryptedAesKey))
                {
                    contentToSend = _encryptionService.EncryptMessage(plainContent, SelectedSession.DecryptedAesKey);
                }

                _logger.LogDebug("Invoking SendMessage on Hub for session {SessionId}", SelectedSession.Id);
                await _hubConnection.InvokeAsync("SendMessage", SelectedSession.Id, contentToSend);
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
            if (session.IsGroupChat && !session.IsAdmin(_authService.CurrentUser?.Id ?? Guid.Empty))
            {
                // Should not happen as UI should hide it, but just in case
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                session.IsGroupChat ? "Are you sure you want to delete this group for everyone?" : "Are you sure you want to delete this chat?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                var response = await _httpClient.DeleteAsync($"{_connectionSettings.ApiBaseUrl.TrimEnd('/')}/api/messages/sessions/{session.Id}");
                if (response.IsSuccessStatusCode)
                {
                    ChatSessions.Remove(session);
                    if (SelectedSession == session) SelectedSession = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete session: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ExitGroupAsync(ChatSessionModel session)
        {
            if (session == null || !session.IsGroupChat) return;

            var confirm = System.Windows.MessageBox.Show("Are you sure you want to exit this group?", "Exit Group", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;

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
                // 1. Generate Session AES Key
                var rawAesKey = _encryptionService.GenerateAesKey();
                
                // 2. Encrypt for all participants (including self)
                var participants = new List<object>();
                
                // Add self
                participants.Add(new { 
                    UserId = CurrentUserId, 
                    EncryptedAesKey = _encryptionService.EncryptAesKeyWithRsa(rawAesKey, _encryptionService.GetPublicKey()) 
                });

                // Add others
                foreach (var contact in SelectedContacts)
                {
                    if (string.IsNullOrEmpty(contact.PublicKey))
                    {
                        // Fetch public key if missing
                        var baseUrl = _connectionSettings.ApiBaseUrl.TrimEnd('/');
                        var keyResponse = await _httpClient.GetFromJsonAsync<PublicKeyResponse>($"{baseUrl}/api/users/{contact.UserId}/public-key");
                        if (keyResponse != null) contact.PublicKey = keyResponse.PublicKey;
                    }

                    if (!string.IsNullOrEmpty(contact.PublicKey))
                    {
                        participants.Add(new { 
                            UserId = contact.UserId, 
                            EncryptedAesKey = _encryptionService.EncryptAesKeyWithRsa(rawAesKey, contact.PublicKey) 
                        });
                    }
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
                System.Windows.MessageBox.Show("Failed to create group. Please try again.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
        }
    }
}
