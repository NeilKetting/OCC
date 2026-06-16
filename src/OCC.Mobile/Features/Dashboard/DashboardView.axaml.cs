using Avalonia.Controls;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void RemainingCard_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            var remainingSection = this.FindControl<Control>("RemainingSection");
            remainingSection?.BringIntoView();
        }

        private void OverdueCard_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            var overdueSection = this.FindControl<Control>("OverdueSection");
            overdueSection?.BringIntoView();
        }
    }
}
