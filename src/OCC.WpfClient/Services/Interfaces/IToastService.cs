namespace OCC.WpfClient.Services.Interfaces
{
    public interface IToastService
    {
        void ShowInfo(string title, string message, bool isSticky = false);
        void ShowSuccess(string title, string message, bool isSticky = false);
        void ShowWarning(string title, string message, bool isSticky = false);
        void ShowError(string title, string message, bool isSticky = false);
    }
}
