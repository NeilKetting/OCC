using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace OCC.Client.Features.AuthHub.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Wait for visual tree to be ready
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is Features.AuthHub.ViewModels.LoginViewModel vm && !string.IsNullOrWhiteSpace(vm.Email))
            {
                this.FindControl<TextBox>("PasswordInput")?.Focus();
            }
            else
            {
                this.FindControl<TextBox>("EmailInput")?.Focus();
            }
        });
    }
}



