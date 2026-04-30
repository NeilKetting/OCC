using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.Services;
using OCC.Mobile.ViewModels;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class InventoryViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private Guid _projectId;

        [ObservableProperty]
        private ObservableCollection<InventoryItem> _items = new();

        public InventoryViewModel(IInventoryService inventoryService, INavigationService navigationService)
        {
            _inventoryService = inventoryService;
            _navigationService = navigationService;
            Title = "Project Inventory";
        }

        [RelayCommand]
        public async Task LoadData()
        {
            IsBusy = true;
            try
            {
                var items = await _inventoryService.GetProjectInventoryAsync(ProjectId);
                Items = new ObservableCollection<InventoryItem>(items);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.GoBack();
        }
    }
}
