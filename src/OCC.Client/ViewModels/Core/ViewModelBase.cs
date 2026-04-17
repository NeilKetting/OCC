using CommunityToolkit.Mvvm.ComponentModel;

namespace OCC.Client.ViewModels.Core
{
    public abstract partial class ViewModelBase : ObservableValidator
    {
        /// <summary>
        /// Gets or sets a value indicating whether the view model is currently performing a long-running operation.
        /// </summary>
        [ObservableProperty]
        private bool _isBusy;

        /// <summary>
        /// Gets or sets the text to display while the view model is busy.
        /// </summary>
        [ObservableProperty]
        private string _busyText = "Please wait...";

        /// <summary>
        /// Gets or sets the title of the view model, often used for headers.
        /// </summary>
        [ObservableProperty]
        private string _title = string.Empty;
    }
}
