namespace OCC.Mobile.Services
{
    public interface IAppInstaller
    {
        Task InstallPackageAsync(string localPath);
    }
}
