using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.WpfClient.Services.Infrastructure
{
    public partial class ConnectionSettings : ObservableObject
    {
        [ObservableProperty]
        private string _apiBaseUrl = "http://102.221.36.149:8081/";

        [ObservableProperty]
        private string _googleApiKey = "";

        [ObservableProperty]
        private AppEnvironment _selectedEnvironment;

        private const string LiveUrl = "https://api.origize63.co.za/";
        private const string TestUrl = "https://api.origize63.co.za/"; // Same port, different database via header
        private const string LocalUrl = "http://localhost:5237/";

        public ConnectionSettings()
        {
            _googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
#if DEBUG
            _selectedEnvironment = AppEnvironment.Local;
            _apiBaseUrl = LocalUrl;
#else
            _selectedEnvironment = AppEnvironment.Test;
            _apiBaseUrl = TestUrl;
#endif
        }

        public enum AppEnvironment
        {
            Live,
            Test,
            Local
        }

        partial void OnSelectedEnvironmentChanged(AppEnvironment value)
        {
            ApiBaseUrl = value switch
            {
                AppEnvironment.Live => LiveUrl,
                AppEnvironment.Test => TestUrl,
                AppEnvironment.Local => LocalUrl,
                _ => ApiBaseUrl
            };
        }
    }
}
