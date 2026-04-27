using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.Services;
using OCC.Mobile.ViewModels;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.HSEQ
{
    public partial class HseqListViewModel : ViewModelBase
    {
        private readonly IHseqService _hseqService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private ObservableCollection<HseqDocument> _documents = new();

        [ObservableProperty]
        private bool _isLoading;

        public HseqListViewModel(IHseqService hseqService, INavigationService navigationService)
        {
            _hseqService = hseqService;
            _navigationService = navigationService;
            Title = "HSEQ Documents";
            
            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadData()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                // In a future version, we can filter by the currently active projects
                // For now, we fetch all documents available to the user
                var docs = await _hseqService.GetDocumentsAsync();
                Documents = new ObservableCollection<HseqDocument>(docs.OrderByDescending(d => d.UploadDate));
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<Dashboard.DashboardViewModel>();
        }

        [RelayCommand]
        private async Task OpenDocument(HseqDocument document)
        {
            // Implementation for opening/downloading the document
            // For now, we just show a message or log it
            await Task.Delay(100); 
        }
    }
}
