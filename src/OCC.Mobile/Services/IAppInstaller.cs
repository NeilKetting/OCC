namespace OCC.Mobile.Services
{
    public interface IAppInstaller
    {
        Task InstallPackageAsync(string localPath);
        Task ShowToastAsync(string message);
    }
}
