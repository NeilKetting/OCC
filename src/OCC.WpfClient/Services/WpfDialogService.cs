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
            var dialog = new CustomDialogView(title, message, "OK", null, null);
            dialog.ShowDialog();
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

        public string? ShowOpenFileDialog(string filter, string title)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        public Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
        {
            var dialog = new OCC.WpfClient.Infrastructure.Views.Dialogs.InputDialogView(title, message, defaultValue);
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult<string?>(dialog.InputValue);
            }
            return Task.FromResult<string?>(null);
        }
    }
}
