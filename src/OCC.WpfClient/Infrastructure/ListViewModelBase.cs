using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using OCC.WpfClient.Services.Interfaces;
using System.Diagnostics;
using System;

namespace OCC.WpfClient.Infrastructure
{
    /// <summary>
    /// Base class for ViewModels that manage a list of items with search and filtering capabilities.
    /// Inherits from OverlayHostViewModel to support modal detail views.
    /// </summary>
    /// <typeparam name="T">The type of item in the list.</typeparam>
    public abstract partial class ListViewModelBase<T> : OverlayHostViewModel
    {
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<T> _items = new();

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private T? _selectedItem;

        protected readonly IPdfService _pdfService;
        public abstract string ReportTitle { get; }
        public abstract List<ReportColumnDefinition> ReportColumns { get; }

        protected ListViewModelBase(IPdfService pdfService)
        {
            _pdfService = pdfService;
        }

        [RelayCommand]
        public async Task PrintAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating report...";
                
                if (_pdfService == null)
                {
                    Debug.WriteLine("Print Error: IPdfService is null. Ensure it is registered and injected correctly.");
                    NotifyError("Print Error", "The PDF generation service is currently unavailable.");
                    return;
                }

                var path = await _pdfService.GenerateListReportPdfAsync(ReportTitle, Items, ReportColumns);
                
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Print Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Orchestrates the data loading and filtering process.
        /// </summary>
        [RelayCommand]
        public async Task LoadData()
        {
            await LoadDataAsync();
        }

        public abstract Task LoadDataAsync();

        /// <summary>
        /// Logic to filter the source list based on SearchQuery and other specific filters.
        /// </summary>
        protected abstract void FilterItems();

        /// <summary>
        /// React to search query changes by re-filtering.
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            FilterItems();
        }
    }
}
