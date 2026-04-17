using Avalonia.Controls;
using Avalonia.Input;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Views.Core
{
    /// <summary>
    /// Code-behind for the SideMenuView.
    /// Handles hitting testing, focus, and view-specific events like DoubleTapped.
    /// </summary>
    public partial class SideMenuView : UserControl
    {
        public SideMenuView()
        {
            InitializeComponent();
        }

        private void Sidebar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Ensure the sidebar gets focus when clicked, helpful for keybindings
            this.Focus();
        }



        /// <summary>
        /// Handles the DoubleTapped event to toggle the sidebar collapse state.
        /// This is linked via XAML: DoubleTapped="Sidebar_DoubleTapped"
        /// </summary>
        private void Sidebar_DoubleTapped(object? sender, TappedEventArgs e)
        {
             if (DataContext is SideMenuViewModel vm)
            {
                vm.ToggleCollapse();
            }
        }
    }
}
