using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Infrastructure.Views.Dialogs;
using System.Threading.Tasks;
using System.Windows;

namespace OCC.WpfClient.Services
{
    public class WpfDialogService : IDialogService
    {
        public Task ShowAlertAsync(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var dialog = new CustomDialogView(title, message, "Yes", null, "No");
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult(dialog.Result == CustomDialogResult.Primary);
            }
            return Task.FromResult(false);
        }

        public Task<CustomDialogResult> ShowConflictResolutionAsync(string title, string message)
        {
            var dialog = new CustomDialogView(title, message, "Force Save", "Reload Latest", "Cancel");
            dialog.ShowDialog();
            return Task.FromResult(dialog.Result);
        }
    }
}
