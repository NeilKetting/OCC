using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.Mobile.Features.Notifications
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly Services.IAuthService _authService;
        private readonly HttpClient _httpClient;

        public string? FCMToken { get; private set; }
        public event EventHandler<string>? TokenChanged;
        public event EventHandler<NotificationEventArgs>? NotificationReceived;

        public PushNotificationService(Services.IAuthService authService)
        {
            _authService = authService;
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
                var baseUrl = "http://102.221.36.149:8081/"; // Production fallback
                if (_httpClient.BaseAddress == null)
                {
                    _httpClient.BaseAddress = new Uri(baseUrl);
                }

                if (!string.IsNullOrEmpty(_authService.CurrentToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
                }

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
                    System.Diagnostics.Debug.WriteLine("[Notifications] Token registered with API successfully.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Notifications] API registration failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
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
