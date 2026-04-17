using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OCC.Client.Features.SettingsHub.Views
{
    public partial class UserDetailView : UserControl
    {
        public UserDetailView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public void FocusInput()
        {
             // Try to find the First Name input. 
             // Note: UserDetailView.axaml Inputs might not be named. 
             // Checking UserDetailView.axaml... Line 57: <TextBox Text="{Binding FirstName}" ... /> No Name.
             // I should probably name it or find by type? Find by Type is safer if no name.
             // But FindControl requires name usually.
             
             // I will add x:Name="FirstNameInput" to the first text box in UserDetailView.axaml 
             // OR just focus the view itself? focus view itself works if KeyBinding is on View.
             // But Textbox focus is better UX.
             var input = this.FindControl<TextBox>("FirstNameInput");
             input?.Focus();
        }
    }
}
