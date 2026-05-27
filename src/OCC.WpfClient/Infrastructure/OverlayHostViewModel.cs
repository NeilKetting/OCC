using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

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
        [NotifyPropertyChangedFor(nameof(IsOverlayActive))]
        [NotifyPropertyChangedFor(nameof(ActiveOverlay))]
        private ViewModelBase? _overlayViewModel;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOverlayActive))]
        [NotifyPropertyChangedFor(nameof(ActiveOverlay))]
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
        /// Standardized method to open an OverlayViewModel with a callback for the result.
        /// </summary>
        public virtual void OpenOverlay(OverlayViewModel viewModel, Action<object?>? callback = null)
        {
            void OnClose(object? sender, object? result)
            {
                viewModel.CloseRequested -= OnClose;
                // Directly close the overlay visually since the VM has already requested and confirmed closure.
                IsOverlayVisible = false;
                callback?.Invoke(result);
            }

            viewModel.CloseRequested += OnClose;
            OpenOverlay((ViewModelBase)viewModel);
        }

        /// <summary>
        /// Standardized method to close the current overlay.
        /// If the active overlay is an OverlayViewModel, it delegates to its Close logic
        /// to ensure dirty checks and closing confirmations are performed.
        /// </summary>
        [RelayCommand]
        public virtual void CloseOverlay()
        {
            if (OverlayViewModel is OverlayViewModel ovm)
            {
                ovm.Close();
            }
            else
            {
                IsOverlayVisible = false;
            }
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
