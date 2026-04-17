using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OCC.Client.Services.Infrastructure
{
    public class ConnectionSettings : INotifyPropertyChanged
    {
        private static ConnectionSettings? _instance;
        public static ConnectionSettings Instance => _instance ??= new ConnectionSettings();

        private bool _useApi = true;
        public bool UseApi
        {
            get => _useApi;
            set
            {
                if (_useApi != value)
                {
                    _useApi = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _apiBaseUrl = "http://102.221.36.149:8081/";
        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set
            {
                if (_apiBaseUrl != value)
                {
                    _apiBaseUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _googleApiKey = "";
        public string GoogleApiKey
        {
            get => _googleApiKey;
            set
            {
                if (_googleApiKey != value)
                {
                    _googleApiKey = value;
                    OnPropertyChanged();
                }
            }
        }

        private ConnectionSettings() 
        { 
            _googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
#if DEBUG
            _selectedEnvironment = AppEnvironment.Live; // Defaulting to Live as requested
            _apiBaseUrl = "http://102.221.36.149:8081/";
#else
            _selectedEnvironment = AppEnvironment.Live;
            _apiBaseUrl = "http://102.221.36.149:8081/";
#endif
        }

        private string? _customLocalUrl;
        public string? CustomLocalUrl
        {
            get => _customLocalUrl;
            set
            {
                if (_customLocalUrl != value)
                {
                    _customLocalUrl = value;
                    if (_selectedEnvironment == AppEnvironment.Local && !string.IsNullOrEmpty(value))
                    {
                        ApiBaseUrl = value;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public enum AppEnvironment
        {
            Live,
            Local
        }

        private AppEnvironment _selectedEnvironment;
        public AppEnvironment SelectedEnvironment
        {
            get => _selectedEnvironment;
            set
            {
                if (_selectedEnvironment != value)
                {
                    _selectedEnvironment = value;
                    switch (_selectedEnvironment)
                    {
                        case AppEnvironment.Live:
                            ApiBaseUrl = "http://102.221.36.149:8081/";
                            break;
                        case AppEnvironment.Local:
                            if (!string.IsNullOrEmpty(_customLocalUrl))
                            {
                                ApiBaseUrl = _customLocalUrl;
                            }
                            else
                            {
                                if (OperatingSystem.IsAndroid())
                                {
                                    ApiBaseUrl = "http://10.0.2.2:5237/";
                                }
                                else
                                {
                                    ApiBaseUrl = "http://localhost:5237/";
                                }
                            }
                            break;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UseLocalDb));
                }
            }
        }

        // Backward compatibility helper if needed, or remove if fully refactored
        public bool UseLocalDb
        {
            get => _selectedEnvironment == AppEnvironment.Local;
            set => SelectedEnvironment = value ? AppEnvironment.Local : AppEnvironment.Live;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
