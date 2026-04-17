using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace OCC.WpfClient.Infrastructure
{
    /// <summary>
    /// Base class for ViewModels that can host a modal/overlay view.
    /// Manages the overlay visibility and provides a standardized property for the overlay content.
    /// </summary>
    public abstract partial class OverlayHostViewModel : ViewModelBase, IOverlayProvider
    {
        /// <inheritdoc />
        public ViewModelBase? ActiveOverlay => IsOverlayVisible ? OverlayViewModel : null;

        [ObservableProperty]
        private ViewModelBase? _overlayViewModel;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOverlayActive))]
        private bool _isOverlayVisible;

        /// <summary>
        /// Global flag to indicate if an overlay is currently being shown.
        /// Can be used by the View to apply blur/dimming effects to the background.
        /// </summary>
        public bool IsOverlayActive => IsOverlayVisible && OverlayViewModel != null;

        /// <summary>
        /// Standardized method to open an overlay.
        /// </summary>
        public virtual void OpenOverlay(ViewModelBase viewModel)
        {
            OverlayViewModel = viewModel;
            IsOverlayVisible = true;
        }

        /// <summary>
        /// Standardized method to close the current overlay.
        /// </summary>
        public virtual void CloseOverlay()
        {
            IsOverlayVisible = false;
            OverlayViewModel = null;
        }

        /// <summary>
        /// Hook for when the overlay property changes, to ensure IsOverlayVisible is synced.
        /// </summary>
        partial void OnOverlayViewModelChanged(ViewModelBase? value)
        {
            IsOverlayVisible = value != null;
        }
    }
}
