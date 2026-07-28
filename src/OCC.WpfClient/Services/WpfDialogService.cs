using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Dialogs;
using System.Threading.Tasks;
using System.Windows;

namespace OCC.WpfClient.Services
{
    public class WpfDialogService : IDialogService
    {
        public Task ShowAlertAsync(string title, string message)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.InvokeAsync(() => ShowAlertAsync(title, message)).Task.Unwrap();
            }

            var dialog = new CustomDialogView(title, message, "OK", null, null);
            dialog.ShowDialog();
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.InvokeAsync(() => ShowConfirmationAsync(title, message)).Task.Unwrap();
            }

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

        public Task<CustomDialogResult> ShowThreeButtonDialogAsync(string title, string message, string primaryText, string secondaryText, string cancelText)
        {
            var dialog = new CustomDialogView(title, message, primaryText, secondaryText, cancelText);
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
            var dialog = new InputDialogView(title, message, defaultValue);
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult<string?>(dialog.InputValue);
            }
            return Task.FromResult<string?>(null);
        }

        public Task<(Guid? ProjectId, string? CustomSite)?> ShowAssignProjectDialogAsync(System.Collections.Generic.List<OCC.Shared.DTOs.ProjectSummaryDto> projects)
        {
            var currentWindow = Application.Current.MainWindow;
            var dialog = new AssignProjectDialogView(projects)
            {
                Owner = currentWindow
            };

            if (dialog.ShowDialog() == true && !dialog.IsCancelled)
            {
                return Task.FromResult<(Guid? ProjectId, string? CustomSite)?>((dialog.SelectedProjectId, dialog.CustomSite));
            }

            return Task.FromResult<(Guid? ProjectId, string? CustomSite)?>(null);
        }
    }
}
