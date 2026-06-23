using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public enum CustomDialogResult
    {
        Primary,
        Secondary,
        Cancel
    }

    public interface IDialogService
    {
        Task ShowAlertAsync(string title, string message);
        Task<bool> ShowConfirmationAsync(string title, string message);
        Task<CustomDialogResult> ShowConflictResolutionAsync(string title, string message);
        string? ShowOpenFileDialog(string filter, string title);
        Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "");
    }
}
