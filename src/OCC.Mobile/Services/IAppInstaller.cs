namespace OCC.Mobile.Services
{
    public interface IAppInstaller
    {
        Task<bool> InstallPackageAsync(string localPath);
        Task ShowToastAsync(string message);
    }
}
