namespace OCC.WpfClient.Infrastructure
{
    /// <summary>
    /// Interface for ViewModels that host an active overlay.
    /// Allows the shell to identify the topmost visible view for context-aware features like bug reporting.
    /// </summary>
    public interface IOverlayProvider
    {
        /// <summary>
        /// Gets the current active overlay ViewModel, or null if no overlay is being shown.
        /// </summary>
        ViewModelBase? ActiveOverlay { get; }
    }
}
