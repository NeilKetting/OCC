using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.ProcurementHub.ViewModels.Dialogs
{
    public partial class ItemNotFoundViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _sku;

        public event Action<bool>? Completed;

        public ItemNotFoundViewModel(string sku)
        {
            Sku = sku;
            Title = "Item Not Found";
        }

        [RelayCommand]
        private void Yes()
        {
            Completed?.Invoke(true);
        }

        [RelayCommand]
        private void No()
        {
            Completed?.Invoke(false);
        }
    }
}
