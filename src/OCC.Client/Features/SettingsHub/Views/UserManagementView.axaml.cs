using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Avalonia;
using Avalonia.Input;
using OCC.Client.Features.SettingsHub.ViewModels;

namespace OCC.Client.Features.SettingsHub.Views
{
    public partial class UserManagementView : UserControl
    {
        public UserManagementView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is UserManagementViewModel vm && vm.IsUserPopupVisible)
                {
                    vm.IsUserPopupVisible = false;
                    e.Handled = true;
                    return;
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var detailView = this.FindControl<UserDetailView>("UserDetailView");
            if (detailView != null)
            {
                detailView.PropertyChanged += async (s, args) =>
                {
                    if (args.Property.Name == nameof(Avalonia.Visual.IsEffectivelyVisible) && args.NewValue is true)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => detailView.FocusInput(), Avalonia.Threading.DispatcherPriority.Background);
                    }
                };
            }
        }

        private void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
             if (DataContext is UserManagementViewModel vm && 
                sender is Avalonia.Controls.DataGrid grid && 
                grid.SelectedItem is OCC.Shared.Models.User user)
            {
                vm.EditUserCommand.Execute(user);
            }
        }
    }
}
