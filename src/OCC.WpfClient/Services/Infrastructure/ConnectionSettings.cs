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

        public ConnectionSettings()
        {
            _googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
#if DEBUG
            _selectedEnvironment = AppEnvironment.Local;
            _apiBaseUrl = "http://localhost:5237/";
#else
            _selectedEnvironment = AppEnvironment.Live;
            _apiBaseUrl = "http://102.221.36.149:8081/";
#endif
        }

        public enum AppEnvironment
        {
            Live,
            Local
        }

        partial void OnSelectedEnvironmentChanged(AppEnvironment value)
        {
            ApiBaseUrl = value switch
            {
                AppEnvironment.Live => "http://102.221.36.149:8081/",
                AppEnvironment.Local => "http://localhost:5237/",
                _ => ApiBaseUrl
            };
        }
    }
}
