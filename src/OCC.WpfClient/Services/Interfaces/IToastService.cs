namespace OCC.WpfClient.Services.Interfaces
{
    public interface IToastService
    {
        void ShowInfo(string title, string message);
        void ShowSuccess(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message);
    }
}
