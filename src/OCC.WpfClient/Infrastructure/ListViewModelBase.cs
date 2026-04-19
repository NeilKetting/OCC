using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

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

        protected ListViewModelBase()
        {
        }

        /// <summary>
        /// Orchestrates the data loading and filtering process.
        /// </summary>
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
