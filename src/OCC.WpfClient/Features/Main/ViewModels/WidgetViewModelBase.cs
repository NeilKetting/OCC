using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Infrastructure;
using System;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public abstract partial class WidgetViewModelBase : ViewModelBase
    {
        [ObservableProperty]
        private string _widgetId = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private int _row;

        [ObservableProperty]
        private int _column;

        [ObservableProperty]
        private int _columnSpan = 1;

        [ObservableProperty]
        private int _rowSpan = 1;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private bool _isEditMode;

        public event EventHandler<string>? LayoutChanged;

        protected void OnLayoutChanged()
        {
            LayoutChanged?.Invoke(this, WidgetId);
        }

        [RelayCommand]
        private void Remove()
        {
            IsVisible = false;
            OnLayoutChanged();
        }

        public abstract System.Threading.Tasks.Task RefreshDataAsync();
    }
}
