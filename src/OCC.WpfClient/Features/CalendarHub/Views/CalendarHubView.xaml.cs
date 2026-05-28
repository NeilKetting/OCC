using System.Windows.Controls;

namespace OCC.WpfClient.Features.CalendarHub.Views
{
    // =========================================================================
    // CalendarHubView.xaml.cs
    // Code-behind for the Calendar hub view.
    // The view is intentionally thin — all logic lives in CalendarHubViewModel.
    // =========================================================================

    /// <summary>
    /// Code-behind for <see cref="CalendarHubView"/>.
    /// No business logic here — the view is fully data-bound via MVVM.
    /// </summary>
    public partial class CalendarHubView : UserControl
    {
        /// <summary>Initialises the CalendarHubView and its XAML components.</summary>
        public CalendarHubView()
        {
            InitializeComponent();
        }
    }
}
