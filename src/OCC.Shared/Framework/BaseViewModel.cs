using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OCC.Shared.Framework
{
    /// <summary>
    /// Cross-platform Base ViewModel class providing observable properties, status messages,
    /// and state management for WPF, Mobile, and Web clients across the OCC ecosystem.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isBusy;
        private string _busyText = "Please wait...";
        private string _title = string.Empty;
        private string? _errorMessage;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void ClearError()
        {
            ErrorMessage = null;
            OnPropertyChanged(nameof(HasError));
        }

        public void SetError(string message)
        {
            ErrorMessage = message;
            OnPropertyChanged(nameof(HasError));
        }
    }
}
