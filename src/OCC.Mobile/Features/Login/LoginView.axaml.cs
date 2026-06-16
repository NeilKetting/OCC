using Avalonia.Controls;
using System;

namespace OCC.Mobile.Features.Login
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            this.SizeChanged += LoginView_SizeChanged;
        }

        private void LoginView_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var leftPanel = this.FindControl<Grid>("LeftPanel");
            var rightPanel = this.FindControl<Grid>("RightPanel");
            var mainGrid = this.FindControl<Grid>("MainGrid");
            
            if (leftPanel != null && rightPanel != null && mainGrid != null)
            {
                if (e.NewSize.Width < 750)
                {
                    // Phone screen: hide left panel, right panel spans full width
                    leftPanel.IsVisible = false;
                    Grid.SetColumnSpan(rightPanel, 2);
                }
                else
                {
                    // Tablet/Desktop screen: show both panels in split layout
                    leftPanel.IsVisible = true;
                    Grid.SetColumnSpan(rightPanel, 1);
                }
            }
        }
    }
}
