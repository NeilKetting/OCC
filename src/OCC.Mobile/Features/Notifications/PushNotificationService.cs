using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.Mobile.Features.Notifications
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly Services.IAuthService _authService;
        private readonly Services.ILocalSettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public string? FCMToken { get; private set; }
        public string Status { get; private set; } = "Initializing...";
        public event EventHandler<string>? TokenChanged;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<NotificationEventArgs>? NotificationReceived;

        public PushNotificationService(Services.IAuthService authService, Services.ILocalSettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        public void Initialize()
        {
            // Initialization logic if needed (e.g. requesting permissions)
        }

        public async void UpdateToken(string token)
        {
            if (FCMToken != token)
            {
                FCMToken = token;
                TokenChanged?.Invoke(this, token);
                System.Diagnostics.Debug.WriteLine($"[Notifications] Token updated: {token}");
            }
            
            // Always attempt registration if authenticated, even if token is the same (to ensure server sync)
            if (_authService.CurrentUser != null && !string.IsNullOrEmpty(token))
            {
                await RegisterTokenWithApi(token);
            }
            else if (string.IsNullOrEmpty(token))
            {
                UpdateStatus("Waiting for Google Token...");
            }
            else
            {
                UpdateStatus("Token ready, waiting for login...");
            }
        }

        public void UpdateStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(this, status);
            System.Diagnostics.Debug.WriteLine($"[Notifications] Status: {status}");
        }

        public async Task RegisterWithApiAsync()
        {
            if (!string.IsNullOrEmpty(FCMToken) && _authService.CurrentUser != null)
            {
                await RegisterTokenWithApi(FCMToken);
            }
        }

        private async Task RegisterTokenWithApi(string token)
        {
            try
            {
                var baseUrl = _authService.GetBaseUrl();
                if (_httpClient.BaseAddress == null)
                {
                    _httpClient.BaseAddress = new Uri(baseUrl);
                }

                // Always use the latest token from AuthService
                _httpClient.DefaultRequestHeaders.Authorization = null;
                if (!string.IsNullOrEmpty(_authService.CurrentToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
                    System.Diagnostics.Debug.WriteLine($"[Notifications] Using Token for User: {_authService.CurrentUser?.Email}");
                }

                _httpClient.DefaultRequestHeaders.Remove("X-Environment");
                _httpClient.DefaultRequestHeaders.Add("X-Environment", _settingsService.Settings.SelectedEnvironment.ToString());

                var platform = "Unknown";
#if ANDROID
                platform = "Android";
#elif IOS
                platform = "iOS";
#endif

                var request = new
                {
                    Token = token,
                    Platform = platform,
                    DeviceName = "Tablet" // Simplified for now
                };

                var response = await _httpClient.PostAsJsonAsync("api/Notifications/register-device", request);
                if (response.IsSuccessStatusCode)
                {
                    UpdateStatus("Registered Successfully");
                    System.Diagnostics.Debug.WriteLine("[Notifications] Token registered with API successfully.");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    UpdateStatus($"API Error: {response.StatusCode} - {error}");
                    System.Diagnostics.Debug.WriteLine($"[Notifications] API registration failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Connection Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Notifications] API Error: {ex.Message}");
            }
        }

        public void HandleNotification(string title, string body)
        {
            NotificationReceived?.Invoke(this, new NotificationEventArgs(title, body));
            System.Diagnostics.Debug.WriteLine($"[Notifications] Received: {title} - {body}");
        }
    }
}
